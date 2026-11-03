using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;

namespace AcxiomCRM.Data;

// Runs at startup: creates the 3 roles and one test user per role.
// Safe to run every time — it only creates what's missing.
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // 1. Roles
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // 2. Test users (DEMO ONLY — moved out of code in Step 14)
        await EnsureUserAsync(userManager, "admin@acxiom.com", "System Admin", "Admin@123", AppRoles.Admin);
        await EnsureUserAsync(userManager, "manager@acxiom.com", "Sales Manager", "Manager@123", AppRoles.Manager);
        await EnsureUserAsync(userManager, "sales@acxiom.com", "Sales Executive", "Sales@123", AppRoles.SalesExecutive);
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
            // Fixes the admin account you registered earlier
            user.IsActive = true;
            if (string.IsNullOrEmpty(user.FullName)) user.FullName = fullName;
            await userManager.UpdateAsync(user);
        }

        if (!await userManager.IsInRoleAsync(user, role))
            await userManager.AddToRoleAsync(user, role);
    }
}