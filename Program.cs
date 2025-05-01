using EsportsTournament.Data; // Ensure the correct namespace for ApplicationDbContext
using EsportsTournament.Models; // Ensure the correct namespace for AspNetUsers
using EsportsTournament.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using EsportsTournament.Interfaces;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllersWithViews();

        // Add DbContext to the container, make sure your connection string is in appsettings.json
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

        // Add Identity and configure it with your custom User class and Entity Framework store
        builder.Services.AddIdentity<AspNetUsers, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        //builder.Services.AddScoped<BracketService>();
        builder.Services.AddScoped<IBracketService, BracketService>();

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/SampleLogin";// Redirect here if not authenticated
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/Account/AccessDenied"; // Optional: for [Authorize(Roles = "...")]
        });

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                // Optional: Configure the cookie options if needed
                options.LoginPath = "/Account/SampleLogin";  // Define the path for the login page
                options.LogoutPath = "/Account/Logout";  // Define the path for the logout action
            });

        // Configure the HTTP request pipeline. 
        var app = builder.Build();
            
        // Exception handling for non-development environment
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        // Static assets and controller route mapping
        app.MapStaticAssets();
        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Index}/{action=Landing}/{id?}")
            .WithStaticAssets();

        app.Run();
    }
}
