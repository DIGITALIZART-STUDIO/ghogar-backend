using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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
            "FinanceManager",
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
                IsActive = true, // Asegurar que el admin esté activo
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

    public static async Task SeedDevelopmentUsers(IServiceProvider serviceProvider, ILogger logger)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<
            RoleManager<IdentityRole<Guid>>
        >();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        // Check if we already have enough users (avoid re-seeding)
        var existingUsersCount = userManager.Users.Count();
        if (existingUsersCount > 100) // If we already have many users, skip seeding
        {
            logger.LogInformation(
                "Development users already seeded ({count} users found), skipping",
                existingUsersCount
            );
            return;
        }

        logger.LogInformation("Starting to seed 1000 development users...");

        // Get role IDs for bulk operations
        var roleIds = await roleManager
            .Roles.Where(r => r.Name != "SuperAdmin")
            .ToDictionaryAsync(r => r.Name!, r => r.Id);

        // Create Bogus faker for generating realistic data
        var userFaker = new Faker<User>("es")
            .RuleFor(u => u.UserName, f => f.Internet.UserName())
            .RuleFor(u => u.Name, f => f.Name.FullName())
            .RuleFor(u => u.Email, f => f.Internet.Email())
            .RuleFor(u => u.PhoneNumber, f => f.Phone.PhoneNumber())
            .RuleFor(u => u.EmailConfirmed, f => f.Random.Bool(0.9f))
            .RuleFor(u => u.MustChangePassword, f => f.Random.Bool(0.1f))
            .RuleFor(u => u.IsActive, f => f.Random.Bool(0.95f))
            .RuleFor(u => u.Id, f => Guid.NewGuid())
            .RuleFor(u => u.SecurityStamp, f => Guid.NewGuid().ToString())
            .RuleFor(u => u.ConcurrencyStamp, f => Guid.NewGuid().ToString());

        var batchSize = 200; // Larger batches for real bulk operations
        var totalUsers = 1000;
        var usersCreated = 0;
        var passwordHasher = new PasswordHasher<User>();

        // Generate all users upfront to avoid duplicates
        var allUsers = new List<User>();
        var usedUsernames = new HashSet<string>();
        var usedEmails = new HashSet<string>();

        // Get existing usernames and emails to avoid conflicts
        var existingUsernames = await userManager.Users.Select(u => u.UserName).ToListAsync();
        var existingEmails = await userManager.Users.Select(u => u.Email).ToListAsync();

        foreach (var username in existingUsernames.Where(u => u != null))
            usedUsernames.Add(username!);
        foreach (var email in existingEmails.Where(e => e != null))
            usedEmails.Add(email!);

        logger.LogInformation("Generating user data...");

        while (allUsers.Count < totalUsers)
        {
            var user = userFaker.Generate();

            // Ensure uniqueness
            if (usedUsernames.Contains(user.UserName) || usedEmails.Contains(user.Email))
            {
                user.UserName = $"user_{Guid.NewGuid().ToString()[..8]}";
                user.Email = $"user_{Guid.NewGuid().ToString()[..8]}@example.com";
            }

            if (!usedUsernames.Contains(user.UserName) && !usedEmails.Contains(user.Email))
            {
                // Hash the password
                user.PasswordHash = passwordHasher.HashPassword(user, "Dev123!456");
                user.NormalizedUserName = user.UserName.ToUpperInvariant();
                user.NormalizedEmail = user.Email.ToUpperInvariant();

                usedUsernames.Add(user.UserName);
                usedEmails.Add(user.Email);
                allUsers.Add(user);
            }
        }

        logger.LogInformation(
            "Generated {count} unique users, inserting in batches...",
            allUsers.Count
        );

        // Insert users in batches
        for (int i = 0; i < allUsers.Count; i += batchSize)
        {
            var batch = allUsers.Skip(i).Take(batchSize).ToList();

            try
            {
                // Bulk insert users
                dbContext.Users.AddRange(batch);
                await dbContext.SaveChangesAsync();

                // Bulk insert user roles
                var userRoles = new List<IdentityUserRole<Guid>>();
                var random = new Random();

                foreach (var user in batch)
                {
                    // Randomly assign 1-2 roles to each user
                    var roleNames = roleIds
                        .Keys.OrderBy(x => Guid.NewGuid())
                        .Take(random.Next(1, 3));

                    foreach (var roleName in roleNames)
                    {
                        userRoles.Add(
                            new IdentityUserRole<Guid>
                            {
                                UserId = user.Id,
                                RoleId = roleIds[roleName],
                            }
                        );
                    }
                }

                dbContext.Set<IdentityUserRole<Guid>>().AddRange(userRoles);
                await dbContext.SaveChangesAsync();

                usersCreated += batch.Count;
                logger.LogInformation(
                    "Inserted batch {batch} - {created}/{total} users created",
                    (i / batchSize) + 1,
                    usersCreated,
                    totalUsers
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error inserting batch {batch}", (i / batchSize) + 1);
                // Remove the failed batch from context to avoid issues
                foreach (var user in batch)
                {
                    dbContext.Entry(user).State = EntityState.Detached;
                }
            }
        }

        logger.LogInformation(
            "Finished seeding development users. Total created: {count}",
            usersCreated
        );
    }
}
