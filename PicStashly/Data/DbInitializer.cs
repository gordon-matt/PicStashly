using Microsoft.AspNetCore.Identity;
using PicStashly.Data;

namespace PicStashly;

/// <summary>
/// Applies pending EF Core migrations on startup (or EnsureCreated for the in-memory fallback),
/// and — when using local ASP.NET Core Identity rather than Keycloak — seeds the built-in
/// roles and a default admin user on first run.
/// </summary>
public static class DbInitializer
{
    /// <summary>
    /// Configuration keys for initial admin user (override via User Secrets or env e.g. SeedAdmin__Email, SeedAdmin__Password).
    /// </summary>
    public const string SeedAdminEmailKey = "SeedAdmin:Email";

    public const string SeedAdminPasswordKey = "SeedAdmin:Password";

    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration, bool useKeycloak)
    {
        var context = services.GetRequiredService<ApplicationDbContextBase>();

        if (context.Database.IsRelational())
        {
            await context.Database.MigrateAsync();
        }
        else
        {
            await context.Database.EnsureCreatedAsync();
        }

        // Keycloak manages users, passwords and roles externally — nothing local to seed.
        if (useKeycloak)
        {
            return;
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

        bool isFirstRun = await SeedRolesAsync(roleManager);
        if (isFirstRun)
        {
            await SeedAdminUserAsync(userManager, configuration);
        }
    }

    private static async Task<bool> SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
    {
        string[] roleNames = [Constants.Roles.Administrator, Constants.Roles.User];

        bool isFirstRun = false;

        foreach (string roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                isFirstRun = true;
                await roleManager.CreateAsync(new ApplicationRole(roleName));
            }
        }

        return isFirstRun;
    }

    private static async Task SeedAdminUserAsync(UserManager<ApplicationUser> userManager, IConfiguration configuration)
    {
        string adminEmail = string.IsNullOrEmpty(configuration[SeedAdminEmailKey])
            ? "admin@picstashly.local"
            : configuration[SeedAdminEmailKey]!;

        string adminPassword = string.IsNullOrEmpty(configuration[SeedAdminPasswordKey])
            ? "Admin@123"
            : configuration[SeedAdminPasswordKey]!;

        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser is null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(adminUser, adminPassword);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, Constants.Roles.Administrator);
            }
        }
        else if (!await userManager.IsInRoleAsync(adminUser, Constants.Roles.Administrator))
        {
            // Ensure existing admin user has the Administrator role
            await userManager.AddToRoleAsync(adminUser, Constants.Roles.Administrator);
        }
    }
}
