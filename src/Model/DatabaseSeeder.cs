using Microsoft.AspNetCore.Identity;

namespace GestionHogar.Model;

public static class DatabaseSeeder
{
    public static async Task SeedDefaultUserAsync(IServiceProvider serviceProvider, ILogger logger)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<
            RoleManager<IdentityRole<Guid>>
        >();

        // Create admin role if it doesn't exist
        // FIXME: get credentials for appsettings, set secure ones in prod
        var adminRoleName = "Admin";
        if (!await roleManager.RoleExistsAsync(adminRoleName))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(adminRoleName));
            logger.LogInformation("Created {role} role", adminRoleName);
        }

        // Create admin user if it doesn't exist
        var adminEmail = "admin@admin.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            adminUser = new User
            {
                UserName = "Admin",
                Email = adminEmail,
                EmailConfirmed = true,
                // Add any additional required fields here
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
