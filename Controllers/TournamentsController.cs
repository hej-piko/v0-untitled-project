// TournamentController.cs
using EsportsTournament.Data;  // Assuming you're using this DbContext
using EsportsTournament.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using EsportsTournament.Services;
using EsportsTournament.Interfaces;

public class TournamentsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<AspNetUsers> _userManager;
    private readonly IBracketService _bracketService;

    // Injecting the necessary services
    public TournamentsController(ApplicationDbContext context, UserManager<AspNetUsers> userManager, IBracketService bracketService)
    {
        _context = context;
        _userManager = userManager;
        _bracketService = bracketService;
    }

    // GET: Tournaments
    public async Task<IActionResult> Index()
    {
        var tournaments = await _context.Tournaments
            .Include(t => t.Creator)
            .OrderByDescending(t => t.StartDate)
            .ToListAsync();

        return View(tournaments);
    }


    // GET: Tournament/Create
    [HttpGet]
    [Authorize]
    public IActionResult Create()
    {
        return View();
    }

    // POST: Tournament/Create
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create(IFormCollection form)
    {
        var name = form["Name"];
        var description = form["Description"];
        var game = form["Game"];
        var startDateStr = form["StartDate"];
        var maxParticipantsStr = form["MaxParticipants"];
        var isOpen = true;
        //var isOpen = form["IsOpen"] == "on";

        if (!DateTime.TryParse(startDateStr, out var startDate))
        {
            ModelState.AddModelError("StartDate", "Invalid start date.");
        }

        if (!int.TryParse(maxParticipantsStr, out var maxParticipants))
        {
            ModelState.AddModelError("MaxParticipants", "Invalid participant number.");
        }

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(game))
        {
            ModelState.AddModelError("", "Name and Game are required.");
        }

        var viewModel = new CreateTournamentViewModel
        {
            Name = name,
            Description = description,
            Game = game,
            StartDate = startDate,
            MaxParticipants = maxParticipants,
            IsOpen = isOpen
        };

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        var creatorId = _userManager.GetUserId(User);

        var tournament = new Tournament
        {
            Name = name,
            Description = description,
            Game = game,
            StartDate = startDate,
            MaxParticipants = maxParticipants,
            IsOpen = isOpen,
            CreatorId = creatorId
        };

        _context.Tournaments.Add(tournament);
        await _context.SaveChangesAsync();

        return RedirectToAction("Details", new { id = tournament.Id });
    }
    

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Details(int id)
    {
        var tournament = await _context.Tournaments
            .Include(t => t.Creator) // Load Creator
            .Include(t => t.Participants)
                .ThenInclude(p => p.User) // Load User inside Participant
            .Include(t => t.Matches) // Load Matches
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tournament == null)
        {
            return NotFound();
        }

        return View(tournament);
    }


    // GET: Tournaments/Edit/5
    [Authorize]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var tournament = await _context.Tournaments.FindAsync(id);
        if (tournament == null)
        {
            return NotFound();
        }

        // Only creator can edit
        if (tournament.CreatorId != _userManager.GetUserId(User))
        {
            return Forbid();
        }

        return View(tournament);
    }

    // POST: Tournaments/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Edit(int id, TournamentEditViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var tournament = await _context.Tournaments.FindAsync(id);
        if (tournament == null)
            return NotFound();

        if (tournament.CreatorId != _userManager.GetUserId(User))
            return Forbid();

        // Update only editable fields
        tournament.Name = model.Name;
        tournament.Game = model.Game;
        tournament.Description = model.Description;
        tournament.StartDate = model.StartDate;
        tournament.MaxParticipants = model.MaxParticipants;
        tournament.IsOpen = model.IsOpen;

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = tournament.Id });
    }


    // POST: Tournaments/Join/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Join(int id)
    {
        var tournament = await _context.Tournaments
            .Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tournament == null)
        {
            return NotFound();
        }

        if (!tournament.IsOpen)
        {
            TempData["Error"] = "This tournament is no longer accepting participants.";
            return RedirectToAction(nameof(Details), new { id = tournament.Id });
        }

        var userId = _userManager.GetUserId(User);

        // Check if user is already participating
        if (tournament.Participants.Any(p => p.UserId == userId))
        {
            TempData["Error"] = "You are already registered for this tournament.";
            return RedirectToAction(nameof(Details), new { id = tournament.Id });
        }

        // Check if tournament is full
        if (tournament.Participants.Count >= tournament.MaxParticipants)
        {
            tournament.IsOpen = false;
            TempData["Error"] = "This tournament is now full.";
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = tournament.Id });
        }

        // Add participant
        var participant = new Participant
        {
            TournamentId = tournament.Id,
            UserId = userId,
            JoinedAt = DateTime.Now
        };

        _context.Participants.Add(participant);
        await _context.SaveChangesAsync();

        // If tournament is now full, close registration and generate bracket
        if (tournament.Participants.Count + 1 >= tournament.MaxParticipants)
        {
            tournament.IsOpen = false;
            await _context.SaveChangesAsync();
            await _bracketService.GenerateBracket(tournament.Id);
        }

        TempData["Success"] = "You have successfully joined the tournament!";
        return RedirectToAction(nameof(Details), new { id = tournament.Id });
    }

    // POST: Tournaments/StartTournament/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> StartTournament(int id)
    {
        var tournament = await _context.Tournaments.FindAsync(id);
        if (tournament == null)
        {
            return NotFound();
        }

        // Only creator can start tournament
        if (tournament.CreatorId != _userManager.GetUserId(User))
        {
            return Forbid();
        }

        if (!tournament.IsOpen)
        {
            TempData["Error"] = "Tournament has already started.";
            return RedirectToAction(nameof(Details), new { id = tournament.Id });
        }

        await _bracketService.GenerateBracket(tournament.Id);
        TempData["Success"] = "Tournament has been started and brackets have been generated!";
        return RedirectToAction(nameof(Details), new { id = tournament.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> UpdateMatch(int matchId, int winnerId, int tournamentId)
    {
        try
        {
            // Use pure ADO.NET to avoid EF Core issues with nulls
            // Open connection manually
            using var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            // STEP 1: Check match existence and get basic data
            bool matchExists = false;
            bool hasCorrectTournament = false;
            string creatorId = "";

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                SELECT 
                    1 as MatchExists,
                    CASE WHEN t.Id = @tournamentId THEN 1 ELSE 0 END as HasCorrectTournament,
                    t.CreatorId
                FROM Matches m
                INNER JOIN Tournaments t ON m.TournamentId = t.Id
                WHERE m.Id = @matchId";

                // Add parameters
                var p1 = cmd.CreateParameter();
                p1.ParameterName = "@matchId";
                p1.Value = matchId;
                cmd.Parameters.Add(p1);

                var p2 = cmd.CreateParameter();
                p2.ParameterName = "@tournamentId";
                p2.Value = tournamentId;
                cmd.Parameters.Add(p2);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    matchExists = true;
                    hasCorrectTournament = reader.GetInt32(reader.GetOrdinal("HasCorrectTournament")) == 1;
                    if (!reader.IsDBNull(reader.GetOrdinal("CreatorId")))
                    {
                        creatorId = reader.GetString(reader.GetOrdinal("CreatorId"));
                    }
                }
            }

            // Check if match exists
            if (!matchExists)
            {
                Debug.WriteLine($"Match with Id {matchId} not found.");
                return NotFound("Match not found");
            }

            // Check tournament association
            if (!hasCorrectTournament)
            {
                Debug.WriteLine($"Match {matchId} does not belong to tournament {tournamentId}");
                return BadRequest("Match does not belong to the specified tournament");
            }

            // Check permissions - user must be creator
            var currentUserId = _userManager.GetUserId(User);
            if (creatorId != currentUserId)
            {
                Debug.WriteLine($"User {currentUserId} is not authorized to update tournament with creator {creatorId}");
                return Forbid();
            }

            // STEP 2: Check participants and validate winner
            bool p1Exists = false;
            bool p2Exists = false;
            int? p1Id = null;
            int? p2Id = null;
            bool isMatchCompleted = false;

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                SELECT 
                    Participant1Id,
                    Participant2Id,
                    IsCompleted
                FROM Matches
                WHERE Id = @matchId";

                var p = cmd.CreateParameter();
                p.ParameterName = "@matchId";
                p.Value = matchId;
                cmd.Parameters.Add(p);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    // Check for null values
                    if (!reader.IsDBNull(reader.GetOrdinal("Participant1Id")))
                    {
                        p1Exists = true;
                        p1Id = reader.GetInt32(reader.GetOrdinal("Participant1Id"));
                    }

                    if (!reader.IsDBNull(reader.GetOrdinal("Participant2Id")))
                    {
                        p2Exists = true;
                        p2Id = reader.GetInt32(reader.GetOrdinal("Participant2Id"));
                    }
                    
                    isMatchCompleted = reader.GetBoolean(reader.GetOrdinal("IsCompleted"));
                }
            }

            // Check if match is already completed
            if (isMatchCompleted)
            {
                Debug.WriteLine($"Match {matchId} is already completed");
                return BadRequest("This match is already completed. You cannot update it.");
            }

            // Check if participants are valid
            if (!p1Exists || !p2Exists)
            {
                Debug.WriteLine($"Match {matchId} has missing participants (P1: {p1Exists}, P2: {p2Exists})");
                return BadRequest("This match doesn't have all participants assigned yet.");
            }

            // Ensure winner is a valid participant
            if (winnerId != p1Id && winnerId != p2Id)
            {
                Debug.WriteLine($"Invalid winnerId: {winnerId}. Must be {p1Id} or {p2Id}");
                return BadRequest("Invalid winner. The winner must be one of the participants in this match.");
            }

            // STEP 3: Update the match directly with SQL
            bool updateSuccess = false;
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                UPDATE Matches
                SET WinnerId = @winnerId, IsCompleted = 1
                WHERE Id = @matchId";

                var p1 = cmd.CreateParameter();
                p1.ParameterName = "@matchId";
                p1.Value = matchId;
                cmd.Parameters.Add(p1);

                var p2 = cmd.CreateParameter();
                p2.ParameterName = "@winnerId";
                p2.Value = winnerId;
                cmd.Parameters.Add(p2);

                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                updateSuccess = (rowsAffected > 0);
            }

            if (!updateSuccess)
            {
                Debug.WriteLine($"Failed to update match {matchId}");
                return StatusCode(500, "Failed to update match");
            }

            // STEP 4: Process bracket updates using the service
            await _bracketService.UpdateMatch(matchId, winnerId);

            TempData["Success"] = "Match result has been updated!";
            return RedirectToAction(nameof(Details), new { id = tournamentId });
        }
        catch (Exception ex)
        {
            // Enhanced error logging
            Debug.WriteLine($"Error in UpdateMatch: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");

            if (ex.InnerException != null)
            {
                Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                Debug.WriteLine($"Inner stack trace: {ex.InnerException.StackTrace}");
            }

            return StatusCode(500, "An error occurred while updating the match. Please try again.");
        }
    }

    private bool TournamentExists(int id)
    {
        return _context.Tournaments.Any(e => e.Id == id);
    }

}
