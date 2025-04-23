using EsportsTournament.Data;
using EsportsTournament.Models;
using EsportsTournament.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace EsportsTournament.Controllers
{
    public class TournamentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly BracketService _bracketService;

        public TournamentsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            BracketService bracketService)
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

        // GET: Tournaments/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tournament = await _context.Tournaments
                .Include(t => t.Creator)
                .Include(t => t.Participants)
                    .ThenInclude(p => p.User)
                .Include(t => t.Matches)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (tournament == null)
            {
                return NotFound();
            }

            return View(tournament);
        }

        // GET: Tournaments/Create
        [Authorize]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Tournaments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create([Bind("Name,Game,Description,StartDate,MaxParticipants")] Tournament tournament)
        {
            if (ModelState.IsValid)
            {
                tournament.CreatorId = _userManager.GetUserId(User);
                tournament.IsOpen = true;
                
                _context.Add(tournament);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Details), new { id = tournament.Id });
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
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Game,Description,StartDate,MaxParticipants,IsOpen")] Tournament tournament)
        {
            if (id != tournament.Id)
            {
                return NotFound();
            }

            var existingTournament = await _context.Tournaments.FindAsync(id);
            if (existingTournament == null)
            {
                return NotFound();
            }

            // Only creator can edit
            if (existingTournament.CreatorId != _userManager.GetUserId(User))
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    existingTournament.Name = tournament.Name;
                    existingTournament.Game = tournament.Game;
                    existingTournament.Description = tournament.Description;
                    existingTournament.StartDate = tournament.StartDate;
                    existingTournament.MaxParticipants = tournament.MaxParticipants;
                    existingTournament.IsOpen = tournament.IsOpen;

                    _context.Update(existingTournament);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TournamentExists(tournament.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Details), new { id = tournament.Id });
            }
            return View(tournament);
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

        // POST: Tournaments/UpdateMatch
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdateMatch(int matchId, int winnerId, int tournamentId)
        {
            var match = await _context.Matches
                .Include(m => m.Tournament)
                .FirstOrDefaultAsync(m => m.Id == matchId);

            if (match == null)
            {
                return NotFound();
            }

            // Only tournament creator can update matches
            if (match.Tournament.CreatorId != _userManager.GetUserId(User))
            {
                return Forbid();
            }

            await _bracketService.UpdateMatch(matchId, winnerId);
            TempData["Success"] = "Match result has been updated!";
            return RedirectToAction(nameof(Details), new { id = tournamentId });
        }

        private bool TournamentExists(int id)
        {
            return _context.Tournaments.Any(e => e.Id == id);
        }
    }
}
