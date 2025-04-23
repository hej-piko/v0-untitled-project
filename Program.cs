using EsportsTournament.Data;
using EsportsTournament.Models;
using EsportsTournament.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Check for production database URL from environment variables (for Vercel)
var productionDbUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(productionDbUrl))
{
    // Parse the connection string from the DATABASE_URL environment variable
    connectionString = ConvertDatabaseUrlToConnectionString(productionDbUrl);
}

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("No database connection string found. Please set either 'DefaultConnection' in appsettings.json or 'DATABASE_URL' environment variable.");
}

// Add this method to the Program.cs file (outside the existing code)
static string ConvertDatabaseUrlToConnectionString(string databaseUrl)
{
    try
    {
        // Parse the URL
        var uri = new Uri(databaseUrl);
        
        // Extract components
        var userInfo = uri.UserInfo.Split(':');
        var username = userInfo[0];
        var password = userInfo.Length > 1 ? userInfo[1] : "";
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 5432; // Default to 5432 for PostgreSQL
        var database = uri.AbsolutePath.TrimStart('/');
        
        // Determine database type from scheme
        if (uri.Scheme.ToLower().Contains("postgres"))
        {
            return $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true;";
        }
        else if (uri.Scheme.ToLower().Contains("sqlserver"))
        {
            return $"Server={host},{port};Initial Catalog={database};User Id={username};Password={password};TrustServerCertificate=True;";
        }
        else
        {
            throw new NotSupportedException($"Database type {uri.Scheme} is not supported");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error parsing DATABASE_URL: {ex.Message}");
        throw;
    }
}

// Keep the existing DbContext setup, but modify it to support both SQL Server and PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    // Check if we're using PostgreSQL
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DATABASE_URL")) && 
        Environment.GetEnvironmentVariable("DATABASE_URL").Contains("postgres"))
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseSqlServer(connectionString);
    }
});

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();

// Register custom services
builder.Services.AddScoped<BracketService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
