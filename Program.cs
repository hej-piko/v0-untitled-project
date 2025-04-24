// Explanation and Fix for Problem 1 (CS0246):
// The error CS0246 indicates that the `ApplicationDbContext` class is not recognized. This typically happens because:
// 1. The `ApplicationDbContext` class is not defined in the project.
// 2. The namespace containing `ApplicationDbContext` is not imported with a `using` directive.

// Fix:
// Ensure that the `ApplicationDbContext` class is defined in your project. If it exists in another namespace, add the appropriate `using` directive at the top of the file. For example:
using EsportsTournament.Data; // Replace 'YourNamespace.Data' with the actual namespace of ApplicationDbContext.
using EsportsTournament.Models;
using Microsoft.AspNetCore.Identity;


// Explanation and Fix for Problem 2 (CS1061):
// The error CS1061 indicates that the `AddDbContext` method is not recognized. This happens because the required Entity Framework Core package is not installed or the necessary `using` directive is missing.

// Fix:
// 1. Install the Entity Framework Core package for SQL Server by running the following command in the terminal:
//    dotnet add package Microsoft.EntityFrameworkCore.SqlServer
// 2. Add the `using` directive for Entity Framework Core at the top of the file:
using Microsoft.EntityFrameworkCore;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllersWithViews();

        // Add DbContext to the container.
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

        builder.Services.AddIdentity<AspNetUsers, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();



        // Configure the HTTP request pipeline.
        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseAuthorization();

        app.MapStaticAssets();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Account}/{action=SampleLogin}/{id?}")
            .WithStaticAssets();

        app.Run();
    }
}