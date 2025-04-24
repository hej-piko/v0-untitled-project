using EsportsTournament.Data;
using EsportsTournament.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Threading.Tasks;

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
        {
            return View(model);
        }

        // Find the user by username using EF Core
        var user = await _context.AspNetUsers
            .FirstOrDefaultAsync(l => l.UserName == model.UserName);

        // If no user is found with that username, return an error
        if (user == null)
        {
            Debug.WriteLine("No account found with the provided credentials.");
            ModelState.AddModelError(string.Empty, "No account found with the provided credentials.");
            return View(model);
        }

        // Check the password (assuming you're using plain text passwords)
        // In reality, you should hash and compare passwords securely.
        if (user.PasswordHash == model.Password) // Replace with proper hash checking for production
        {
            // User authenticated successfully
            return RedirectToAction("Index", "Home");
        }
        else
        {
            Debug.WriteLine("Invalid login attempt.");
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }
    }


    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("SampleLogin", "Account");
    }
}
