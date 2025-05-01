using EsportsTournament.Data;
using EsportsTournament.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
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
