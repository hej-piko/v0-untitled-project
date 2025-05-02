using EsportsTournament.Data;
using EsportsTournament.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNet.Identity.Owin;

public class AccountController : Controller
{
    private readonly SignInManager<AspNetUsers> _signInManager;
    private readonly UserManager<AspNetUsers> _userManager;
    private readonly ApplicationDbContext _context;  // Assuming you have a context for DB access

    // Injecting the necessary services into the constructor
    public AccountController(SignInManager<AspNetUsers> signInManager,
                             UserManager<AspNetUsers> userManager,
                             ApplicationDbContext context)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _context = context;  // DB context injected
    }

    [HttpGet]
    public IActionResult SampleLogin()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SampleLogin(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.UserName,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: false
        );

        if (result.Succeeded)
        {
            return RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return View(model);
    }

    [Authorize] // Ensure only logged-in users can access
    public async Task<IActionResult> Dashboard()
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null)
        {
            // Should not happen if [Authorize] is working, but good practice
            return Challenge(); 
        }

        var hostedTournaments = await _context.Tournaments
            .Include(t => t.Participants) // Eager load Participants
            .Where(t => t.CreatorId == userId)
            .OrderByDescending(t => t.StartDate)
            .ToListAsync();

        var joinedTournaments = await _context.Tournaments
                                     .Include(t => t.Participants) 
                                     .Include(t => t.Creator)      
                                     .Where(t => t.Participants != null && t.Participants.Any(p => p.UserId == userId)) 
                                     .OrderByDescending(t => t.StartDate)
                                     .ToListAsync(); 

        var viewModel = new DashboardViewModel
        {
            HostedTournaments = hostedTournaments,
            JoinedTournaments = joinedTournaments 
        };

        return View(viewModel);
    }

    public async Task<IActionResult> Logout()
    {
        
        await _signInManager.SignOutAsync();
        return RedirectToAction("Landing", "Index");
    }


    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new AspNetUsers
        {
            UserName = model.UserName,
            Email = model.Email,
            DisplayName = model.DisplayName, // Save the custom DisplayName
            LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(0)
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            return RedirectToAction("SampleLogin", "Account");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError("", error.Description);
        }

        return View(model);
    }


}
