using Microsoft.AspNetCore.Identity;

namespace GestionHogar.Model;

public static class DatabaseSeeder
{
    public static async Task SeedRoles(IServiceProvider serviceProvider, ILogger logger)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<
            RoleManager<IdentityRole<Guid>>
        >();

        var roles = new List<string>
        {
            "SuperAdmin",
            "Admin",
            "Supervisor",
            "SalesAdvisor",
            "Manager",
        };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                logger.LogInformation("Created {role} role", role);
            }
        }
    }

    public static async Task SeedDefaultUserAsync(IServiceProvider serviceProvider, ILogger logger)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<
            RoleManager<IdentityRole<Guid>>
        >();

        // Create admin user if it doesn't exist
        // FIXME: get credentials from appsettings, set secure ones in prod
        var adminEmail = "admin@admin.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        var adminRoleName = "SuperAdmin";

        if (adminUser == null)
        {
            adminUser = new User
            {
                UserName = "admin",
                Name = "Administrador",
                Email = adminEmail,
                EmailConfirmed = true,
                MustChangePassword = false,
            };

            // Create the admin user with a password
            var result = await userManager.CreateAsync(adminUser, "Hogar2025/1");

            if (result.Succeeded)
            {
                // Add admin role to user
                await userManager.AddToRoleAsync(adminUser, adminRoleName);
                logger.LogInformation("Created admin user with email {email}", adminEmail);
            }
            else
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new Exception($"Failed to create admin user. Errors: {errors}");
            }
        }
    }
}
