using EsportsTournament.Data;
using EsportsTournament.Models;
using Microsoft.EntityFrameworkCore;
using EsportsTournament.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Data.SqlTypes;
using System.Data;
using Microsoft.Data.SqlClient;

namespace EsportsTournament.Services
{
    public class BracketService : IBracketService
    {
        private readonly ApplicationDbContext _context;

        public BracketService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task GenerateBracket(int tournamentId)
        {
            var tournament = await _context.Tournaments
                .Include(t => t.Participants)
                .FirstOrDefaultAsync(t => t.Id == tournamentId);

            if (tournament == null)
                throw new ArgumentException("Tournament not found");

            // Close tournament registration
            tournament.IsOpen = false;

            // Get participants and assign seeds based on join time (first come, first serve)
            var participants = tournament.Participants
                .OrderBy(p => p.JoinedAt)
                .ToList();

            for (int i = 0; i < participants.Count; i++)
            {
                participants[i].Seed = i + 1;
            }

            // Calculate number of rounds needed
            int participantCount = participants.Count;
            int rounds = (int)Math.Ceiling(Math.Log(participantCount, 2));
            int totalMatches = (int)Math.Pow(2, rounds) - 1;
            int firstRoundMatches = (int)Math.Pow(2, rounds - 1);
            int byes = (int)Math.Pow(2, rounds) - participantCount;

            // Create matches
            List<Match> matches = new List<Match>();

            // First round matches
            for (int i = 0; i < firstRoundMatches; i++)
            {
                var match = new Match
                {
                    TournamentId = tournamentId,
                    Round = 1,
                    MatchNumber = i + 1
                };

                // Assign participants using seeding
                if (i < participantCount)
                {
                    // Use standard tournament seeding pattern
                    int seed1 = GetSeedForPosition(i, firstRoundMatches);
                    if (seed1 <= participantCount)
                    {
                        match.Participant1Id = participants.FirstOrDefault(p => p.Seed == seed1)?.Id;
                    }

                    int seed2 = GetSeedForPosition(i + firstRoundMatches, firstRoundMatches);
                    if (seed2 <= participantCount)
                    {
                        match.Participant2Id = participants.FirstOrDefault(p => p.Seed == seed2)?.Id;
                    }

                    // If only one participant is assigned (bye), automatically advance them
                    if (match.Participant1Id.HasValue && !match.Participant2Id.HasValue)
                    {
                        match.WinnerId = match.Participant1Id;
                        match.IsCompleted = true;
                    }
                    else if (!match.Participant1Id.HasValue && match.Participant2Id.HasValue)
                    {
                        match.WinnerId = match.Participant2Id;
                        match.IsCompleted = true;
                    }
                }

                matches.Add(match);
            }

            // Create placeholder matches for subsequent rounds
            for (int round = 2; round <= rounds; round++)
            {
                int matchesInRound = (int)Math.Pow(2, rounds - round);
                for (int i = 0; i < matchesInRound; i++)
                {
                    matches.Add(new Match
                    {
                        TournamentId = tournamentId,
                        Round = round,
                        MatchNumber = i + 1   // MatchNumber resets for each round
                    });
                }
            }

            await _context.Matches.AddRangeAsync(matches);
            await _context.SaveChangesAsync();

            // Process auto-advances from first round
            var completedFirstRoundMatches = matches
                .Where(m => m.Round == 1 && m.IsCompleted && m.WinnerId.HasValue)
                .ToList();

            foreach (var completedMatch in completedFirstRoundMatches)
            {
                await UpdateNextMatchParticipant(completedMatch.Id, completedMatch.WinnerId.Value);
            }
        }

        private int GetSeedForPosition(int position, int totalPositions)
        {
            // Standard tournament seeding algorithm
            if (position < 2)
                return position + 1;

            int power = 2;
            while (power * 2 <= position)
                power *= 2;

            return 2 * (position - power + 1);
        }

        public async Task UpdateMatch(int matchId, int winnerId)
        {
            var match = await _context.Matches
                .Include(m => m.Tournament)
                .FirstOrDefaultAsync(m => m.Id == matchId);

            if (match == null)
                throw new ArgumentException("Match not found");

            // Validate winner is a participant in this match
            if (match.Participant1Id != winnerId && match.Participant2Id != winnerId)
                throw new ArgumentException("Invalid winner");

            // Update current match
            match.WinnerId = winnerId;
            match.IsCompleted = true;

            // Save the current match update
            await _context.SaveChangesAsync();

            // Handle advancing to next match
            await UpdateNextMatchParticipant(matchId, winnerId);
        }

        private async Task UpdateNextMatchParticipant(int currentMatchId, int winnerId)
        {
            try
            {
                // Get the connection string from the context
                string connectionString = _context.Database.GetConnectionString();
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("Database connection string is not configured properly");
                }

                // Initialize variables
                int? currentMatchRound = null;
                int? currentMatchNumber = null;
                int? tournamentId = null;
                int? maxParticipants = null;

                // Create and open a new connection
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Step 1: Get current match details
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT m.Round, m.MatchNumber, m.TournamentId, t.MaxParticipants
                            FROM Matches m
                            INNER JOIN Tournaments t ON m.TournamentId = t.Id
                            WHERE m.Id = @matchId";

                        cmd.Parameters.Add(new SqlParameter("@matchId", currentMatchId));

                        using var reader = await cmd.ExecuteReaderAsync();
                        if (await reader.ReadAsync())
                        {
                            currentMatchRound = reader.GetInt32(0);
                            currentMatchNumber = reader.GetInt32(1);
                            tournamentId = reader.GetInt32(2);
                            maxParticipants = reader.GetInt32(3);
                        }
                    }

                    // Verify we have the data we need
                    if (!currentMatchRound.HasValue || !currentMatchNumber.HasValue ||
                        !tournamentId.HasValue || !maxParticipants.HasValue)
                        return;

                    // Calculate total rounds in tournament
                    int totalRounds = (int)Math.Ceiling(Math.Log(maxParticipants.Value, 2));

                    // Stop if this is the final round
                    if (currentMatchRound.Value >= totalRounds)
                        return;

                    // Calculate next round and match
                    int nextRound = currentMatchRound.Value + 1;
                    // This is the correct calculation for determining the next match
                    int nextMatchNumber = (int)Math.Ceiling(currentMatchNumber.Value / 2.0);

                    // Get the next match ID
                    int? nextMatchId = null;
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT Id
                            FROM Matches
                            WHERE TournamentId = @tournamentId 
                            AND Round = @round 
                            AND MatchNumber = @matchNumber";

                        cmd.Parameters.Add(new SqlParameter("@tournamentId", tournamentId.Value));
                        cmd.Parameters.Add(new SqlParameter("@round", nextRound));
                        cmd.Parameters.Add(new SqlParameter("@matchNumber", nextMatchNumber));

                        object result = await cmd.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                        {
                            nextMatchId = Convert.ToInt32(result);
                        }
                    }

                    if (!nextMatchId.HasValue)
                        return;

                    // Determine which participant slot to update based on the current match number
                    // Odd match winners go to Participant1 slot, even match winners go to Participant2 slot
                    bool isOdd = currentMatchNumber.Value % 2 != 0;

                    // Log for debugging
                    Debug.WriteLine($"Current Match: {currentMatchId}, Number: {currentMatchNumber}, Round: {currentMatchRound}");
                    Debug.WriteLine($"Next Match: {nextMatchId}, Round: {nextRound}, Position: {(isOdd ? "Participant1" : "Participant2")}");

                    // Update the appropriate participant slot in the next match
                    using (var cmd = connection.CreateCommand())
                    {
                        if (isOdd)
                        {
                            cmd.CommandText = @"
                                UPDATE Matches
                                SET Participant1Id = @winnerId
                                WHERE Id = @matchId";
                        }
                        else
                        {
                            cmd.CommandText = @"
                                UPDATE Matches
                                SET Participant2Id = @winnerId
                                WHERE Id = @matchId";
                        }

                        cmd.Parameters.Add(new SqlParameter("@winnerId", winnerId));
                        cmd.Parameters.Add(new SqlParameter("@matchId", nextMatchId.Value));

                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Now check if we need to auto-advance (if both participants are filled)
                    int? p1Id = null;
                    int? p2Id = null;

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT Participant1Id, Participant2Id
                            FROM Matches
                            WHERE Id = @matchId";

                        cmd.Parameters.Add(new SqlParameter("@matchId", nextMatchId.Value));

                        using var reader = await cmd.ExecuteReaderAsync();
                        if (await reader.ReadAsync())
                        {
                            p1Id = !reader.IsDBNull(0) ? reader.GetInt32(0) : null;
                            p2Id = !reader.IsDBNull(1) ? reader.GetInt32(1) : null;
                        }
                    }

                    // Auto-advance scenarios
                    if (p1Id.HasValue && p2Id.HasValue && p1Id.Value == p2Id.Value)
                    {
                        // Extremely rare case: both participants are the same
                        using (var cmd = connection.CreateCommand())
                        {
                            cmd.CommandText = @"
                                UPDATE Matches
                                SET WinnerId = @winnerId, IsCompleted = 1
                                WHERE Id = @matchId";

                            cmd.Parameters.Add(new SqlParameter("@winnerId", p1Id.Value));
                            cmd.Parameters.Add(new SqlParameter("@matchId", nextMatchId.Value));

                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Recursive call to keep advancing
                        await UpdateNextMatchParticipant(nextMatchId.Value, p1Id.Value);
                    }
                    else if (currentMatchRound > 1)
                    {
                        // For rounds after the first, handle byes by auto-advancing
                        if (p1Id.HasValue && !p2Id.HasValue)
                        {
                            // Auto-advance participant 1
                            using (var cmd = connection.CreateCommand())
                            {
                                cmd.CommandText = @"
                                    UPDATE Matches
                                    SET WinnerId = @winnerId, IsCompleted = 1
                                    WHERE Id = @matchId";

                                cmd.Parameters.Add(new SqlParameter("@winnerId", p1Id.Value));
                                cmd.Parameters.Add(new SqlParameter("@matchId", nextMatchId.Value));

                                await cmd.ExecuteNonQueryAsync();
                            }

                            await UpdateNextMatchParticipant(nextMatchId.Value, p1Id.Value);
                        }
                        else if (!p1Id.HasValue && p2Id.HasValue)
                        {
                            // Auto-advance participant 2
                            using (var cmd = connection.CreateCommand())
                            {
                                cmd.CommandText = @"
                                    UPDATE Matches
                                    SET WinnerId = @winnerId, IsCompleted = 1
                                    WHERE Id = @matchId";

                                cmd.Parameters.Add(new SqlParameter("@winnerId", p2Id.Value));
                                cmd.Parameters.Add(new SqlParameter("@matchId", nextMatchId.Value));

                                await cmd.ExecuteNonQueryAsync();
                            }

                            await UpdateNextMatchParticipant(nextMatchId.Value, p2Id.Value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the exception
                Debug.WriteLine($"Error in UpdateNextMatchParticipant: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                // Re-throw to notify calling code
                throw;
            }
        }
    }
}