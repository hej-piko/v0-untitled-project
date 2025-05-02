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
                        MatchNumber = i + 1   // ✅ MatchNumber resets for each round
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
            Match currentMatch = null;
            Tournament currentTournament = null;

            using var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            // Step 1: Get current match and tournament info
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
            SELECT m.Id, m.Round, m.MatchNumber, m.TournamentId, t.MaxParticipants
            FROM Matches m
            INNER JOIN Tournaments t ON m.TournamentId = t.Id
            WHERE m.Id = @matchId";

                var p = cmd.CreateParameter();
                p.ParameterName = "@matchId";
                p.Value = currentMatchId;
                cmd.Parameters.Add(p);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    currentMatch = new Match
                    {
                        Id = reader.GetInt32(0),
                        Round = reader.GetInt32(1),
                        MatchNumber = reader.GetInt32(2),
                        TournamentId = reader.GetInt32(3)
                    };
                    currentTournament = new Tournament
                    {
                        MaxParticipants = reader.GetInt32(4)
                    };
                }
            }

            if (currentMatch == null) return;

            // Step 2: Calculate next match position
            int totalRounds = (int)Math.Ceiling(Math.Log(currentTournament.MaxParticipants, 2));
            if (currentMatch.Round >= totalRounds) return;

            int nextRound = currentMatch.Round + 1;
            int nextMatchNumber = ((currentMatch.MatchNumber - 1) / 2) + 1;

            int? nextMatchId = null;
            int? nextP1Id = null;
            int? nextP2Id = null;

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
            SELECT Id, Participant1Id, Participant2Id, IsCompleted
            FROM Matches
            WHERE TournamentId = @tournamentId AND Round = @round AND MatchNumber = @matchNumber";

                var p1 = cmd.CreateParameter(); p1.ParameterName = "@tournamentId"; p1.Value = currentMatch.TournamentId; cmd.Parameters.Add(p1);
                var p2 = cmd.CreateParameter(); p2.ParameterName = "@round"; p2.Value = nextRound; cmd.Parameters.Add(p2);
                var p3 = cmd.CreateParameter(); p3.ParameterName = "@matchNumber"; p3.Value = nextMatchNumber; cmd.Parameters.Add(p3);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    nextMatchId = reader.GetInt32(0);
                    if (!reader.IsDBNull(1)) nextP1Id = reader.GetInt32(1);
                    if (!reader.IsDBNull(2)) nextP2Id = reader.GetInt32(2);
                }
            }

            if (nextMatchId == null) return;

            // Step 3: Add winner to the correct slot
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
            UPDATE Matches
            SET 
                Participant1Id = CASE WHEN @isOdd = 1 THEN @winnerId ELSE Participant1Id END,
                Participant2Id = CASE WHEN @isOdd = 0 THEN @winnerId ELSE Participant2Id END
            WHERE Id = @matchId";

                var p1 = cmd.CreateParameter(); p1.ParameterName = "@isOdd"; p1.Value = (currentMatch.MatchNumber % 2 != 0) ? 1 : 0; cmd.Parameters.Add(p1);
                var p2 = cmd.CreateParameter(); p2.ParameterName = "@winnerId"; p2.Value = winnerId; cmd.Parameters.Add(p2);
                var p3 = cmd.CreateParameter(); p3.ParameterName = "@matchId"; p3.Value = nextMatchId.Value; cmd.Parameters.Add(p3);

                await cmd.ExecuteNonQueryAsync();
            }

            // Step 4: Re-check participants for next match after update
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT Participant1Id, Participant2Id FROM Matches WHERE Id = @id";
                var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.Value = nextMatchId.Value; cmd.Parameters.Add(p);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    nextP1Id = !reader.IsDBNull(0) ? reader.GetInt32(0) : null;
                    nextP2Id = !reader.IsDBNull(1) ? reader.GetInt32(1) : null;
                }
            }

            // Step 5: Auto-complete if both participants are the same
            if (nextP1Id.HasValue && nextP2Id.HasValue && nextP1Id == nextP2Id)
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
                UPDATE Matches
                SET WinnerId = @winnerId, IsCompleted = 1
                WHERE Id = @matchId";

                    var p1 = cmd.CreateParameter(); p1.ParameterName = "@winnerId"; p1.Value = nextP1Id.Value; cmd.Parameters.Add(p1);
                    var p2 = cmd.CreateParameter(); p2.ParameterName = "@matchId"; p2.Value = nextMatchId.Value; cmd.Parameters.Add(p2);

                    await cmd.ExecuteNonQueryAsync();
                }

                // Recursive call
                await UpdateNextMatchParticipant(nextMatchId.Value, nextP1Id.Value);
            }

            // Optional: handle one-bye advancement for later rounds only
            if ((nextP1Id != null && nextP2Id == null) || (nextP1Id == null && nextP2Id != null))
            {
                if (currentMatch.Round > 2)
                {
                    int autoWinnerId = nextP1Id ?? nextP2Id ?? winnerId;

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                    UPDATE Matches
                    SET WinnerId = @winnerId, IsCompleted = 1
                    WHERE Id = @matchId";

                        var p1 = cmd.CreateParameter(); p1.ParameterName = "@winnerId"; p1.Value = autoWinnerId; cmd.Parameters.Add(p1);
                        var p2 = cmd.CreateParameter(); p2.ParameterName = "@matchId"; p2.Value = nextMatchId.Value; cmd.Parameters.Add(p2);

                        await cmd.ExecuteNonQueryAsync();
                    }

                    await UpdateNextMatchParticipant(nextMatchId.Value, autoWinnerId);
                }
            }
        }
    }
}