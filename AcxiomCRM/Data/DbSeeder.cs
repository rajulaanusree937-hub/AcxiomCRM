using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Data;

// Runs at startup:
//  0. Creates/updates the database tables (needed on a fresh Azure SQL database)
//  1. Creates the 3 roles
//  2. Demo users — DEVELOPMENT ONLY
//  3. First administrator for a real deployment — from secure settings, never hard-coded
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, bool includeDemoUsers)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // 0. Apply any pending migrations (no-op if the database is already up to date)
        await db.Database.MigrateAsync();

        // 1. Roles
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // 2. Demo users — Development only (Step 14)

        if (includeDemoUsers)
        {
            await EnsureUserAsync(userManager, "admin@acxiom.com", "System Admin", "Admin@123", AppRoles.Admin);
            await EnsureUserAsync(userManager, "manager@acxiom.com", "Sales Manager", "Manager@123", AppRoles.Manager);
            await EnsureUserAsync(userManager, "sales@acxiom.com", "Sales Executive", "Sales@123", AppRoles.SalesExecutive);
        }

        // 3. First admin for a deployed environment (values come from Azure App Settings / env variables)
        var adminEmail = config["SeedAdmin:Email"];
        var adminPassword = config["SeedAdmin:Password"];
        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
            await EnsureUserAsync(userManager, adminEmail, "Administrator", adminPassword, AppRoles.Admin);
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email, string fullName, string password, string role)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                IsActive = true
            };

            // CreateAsync hashes the password — plain text is never stored

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Seeding {email} failed: " +
                    string.Join(", ", result.Errors.Select(e => e.Description)));
        }
        else if (!user.IsActive || string.IsNullOrEmpty(user.FullName))
        {
            user.IsActive = true;
            if (string.IsNullOrEmpty(user.FullName)) user.FullName = fullName;
            await userManager.UpdateAsync(user);
        }

        if (!await userManager.IsInRoleAsync(user, role))
            await userManager.AddToRoleAsync(user, role);
    }
}