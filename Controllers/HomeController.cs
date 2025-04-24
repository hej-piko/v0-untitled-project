using EsportsTournament.Data;
using EsportsTournament.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace EsportsTournament.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            //var upcomingTournaments = await _context.Tournaments
            //    .Where(t => t.IsOpen)
            //    .OrderBy(t => t.StartDate)
            //    .Take(5)
            //    .ToListAsync();

            //var activeTournaments = await _context.Tournaments
            //    .Where(t => !t.IsOpen)
            //    .OrderByDescending(t => t.StartDate)
            //    .Take(5)
            //    .ToListAsync();

            //ViewBag.UpcomingTournaments = upcomingTournaments;
            //ViewBag.ActiveTournaments = activeTournaments;

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
