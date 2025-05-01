using EsportsTournament.Data;
using EsportsTournament.Models;
using Microsoft.EntityFrameworkCore;
using EsportsTournament.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

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
            int matchNumber = firstRoundMatches + 1;
            for (int round = 2; round <= rounds; round++)
            {
                int matchesInRound = (int)Math.Pow(2, rounds - round);
                for (int i = 0; i < matchesInRound; i++)
                {
                    matches.Add(new Match
                    {
                        TournamentId = tournamentId,
                        Round = round,
                        MatchNumber = matchNumber++
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
            var currentMatch = await _context.Matches
                .Include(m => m.Tournament)
                .FirstOrDefaultAsync(m => m.Id == currentMatchId);

            if (currentMatch == null)
                return;

            // Check if this is the final match
            int totalRounds = (int)Math.Ceiling(Math.Log(currentMatch.Tournament?.MaxParticipants ?? 2, 2));
            if (currentMatch.Round >= totalRounds)
            {
                // This is the final match, no next match to update
                Debug.WriteLine($"Final match {currentMatchId} completed. Tournament winner: {winnerId}");
                return;
            }

            // Calculate the next match number correctly
            // For round 1, matches 1-2 go to round 2 match 1, matches 3-4 go to round 2 match 2, etc.
            int nextMatchNumber = ((currentMatch.MatchNumber - 1) / 2) + 1;
            int nextRound = currentMatch.Round + 1;

            Debug.WriteLine($"Current match: {currentMatchId} (Round {currentMatch.Round}, Number {currentMatch.MatchNumber})");
            Debug.WriteLine($"Next match should be: Round {nextRound}, Number {nextMatchNumber}");

            var nextMatch = await _context.Matches
                .FirstOrDefaultAsync(m =>
                    m.TournamentId == currentMatch.TournamentId &&
                    m.Round == nextRound &&
                    m.MatchNumber == nextMatchNumber);

            if (nextMatch != null)
            {
                Debug.WriteLine($"Found next match: {nextMatch.Id}");

                // Set winner as participant in the next match
                // If current match number is odd, winner goes to participant1 slot
                // If current match number is even, winner goes to participant2 slot
                if (currentMatch.MatchNumber % 2 != 0) // Odd match number
                {
                    nextMatch.Participant1Id = winnerId;
                    Debug.WriteLine($"Assigned winner {winnerId} to Participant1 slot");
                }
                else // Even match number
                {
                    nextMatch.Participant2Id = winnerId;
                    Debug.WriteLine($"Assigned winner {winnerId} to Participant2 slot");
                }

                // Auto-complete next match if both participants are assigned
                // but one is null (bye scenario)
                if (nextMatch.Participant1Id.HasValue && !nextMatch.Participant2Id.HasValue)
                {
                    nextMatch.WinnerId = nextMatch.Participant1Id;
                    nextMatch.IsCompleted = true;
                    Debug.WriteLine($"Auto-advancing Participant1 {nextMatch.Participant1Id} due to bye");
                }
                else if (!nextMatch.Participant1Id.HasValue && nextMatch.Participant2Id.HasValue)
                {
                    nextMatch.WinnerId = nextMatch.Participant2Id;
                    nextMatch.IsCompleted = true;
                    Debug.WriteLine($"Auto-advancing Participant2 {nextMatch.Participant2Id} due to bye");
                }

                await _context.SaveChangesAsync();

                // If we auto-advanced, also update the next match in the sequence
                if (nextMatch.IsCompleted && nextMatch.WinnerId.HasValue)
                {
                    await UpdateNextMatchParticipant(nextMatch.Id, nextMatch.WinnerId.Value);
                }
            }
            else
            {
                Debug.WriteLine($"ERROR: Next match not found for round {nextRound}, number {nextMatchNumber}");
            }
        }
    }
}