using EsportsTournament.Data;
using EsportsTournament.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EsportsTournament.Services
{
    public class BracketService
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
            if (winnerId != match.Participant1Id && winnerId != match.Participant2Id)
                throw new ArgumentException("Invalid winner");

            // Update match with winner
            match.WinnerId = winnerId;
            match.IsCompleted = true;

            // Find the next match where this winner should advance
            int nextRound = match.Round + 1;
            int matchesPerRound = (int)Math.Pow(2, Math.Log(match.Tournament.MaxParticipants, 2) - match.Round + 1);
            int nextMatchNumber = (match.MatchNumber - 1) / 2 + matchesPerRound + 1;

            var nextMatch = await _context.Matches
                .FirstOrDefaultAsync(m => m.TournamentId == match.TournamentId && 
                                         m.Round == nextRound && 
                                         m.MatchNumber == nextMatchNumber);

            if (nextMatch != null)
            {
                // Determine which participant slot to fill
                if (match.MatchNumber % 2 == 1)
                {
                    nextMatch.Participant1Id = winnerId;
                }
                else
                {
                    nextMatch.Participant2Id = winnerId;
                }

                // If both participants are set and one is null (bye), auto-advance
                if (nextMatch.Participant1Id.HasValue && !nextMatch.Participant2Id.HasValue)
                {
                    nextMatch.WinnerId = nextMatch.Participant1Id;
                    nextMatch.IsCompleted = true;
                }
                else if (!nextMatch.Participant1Id.HasValue && nextMatch.Participant2Id.HasValue)
                {
                    nextMatch.WinnerId = nextMatch.Participant2Id;
                    nextMatch.IsCompleted = true;
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}
