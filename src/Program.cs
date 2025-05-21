using GestionHogar.Controllers;
using GestionHogar.Model;
using GestionHogar.Utils;
using GestionHogar.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddControllers();

//
// Configuration setup
//
builder.Services.Configure<CorsConfiguration>(
    builder.Configuration.GetSection("Cors")
);

// Database setup
builder.Services.AddDbContext<DatabaseContext>(options =>
{
    var connectionString =
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new Exception("DB connection string not found");
    options.UseNpgsql(connectionString);
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowOrigins",
        policy =>
        {
            var corsSettings = builder.Services.BuildServiceProvider()
                .GetRequiredService<IOptions<CorsConfiguration>>().Value;
            var allowedOrigins = corsSettings.AllowedOrigins;

            if (allowedOrigins == null || allowedOrigins.Length == 0)
            {
                throw new Exception("Allowed origins not found in configuration");
            }

            policy
                .WithOrigins(allowedOrigins)
                .AllowCredentials()
                .AllowAnyMethod()
                .AllowAnyHeader()
                .SetIsOriginAllowedToAllowWildcardSubdomains();
        }
    );
});

// Configure Identity
builder
    .Services.AddIdentityCore<User>(options =>
    {
        // Password settings
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;

        // Email settings
        options.User.RequireUniqueEmail = true;

        // Lockout settings
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<DatabaseContext>()
    .AddDefaultTokenProviders();

// Add authentication
builder
    .Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        var corsSettings = builder.Services.BuildServiceProvider()
            .GetRequiredService<IOptions<CorsConfiguration>>().Value;

        options.Cookie.Name = corsSettings.CookieName;
        options.ExpireTimeSpan = TimeSpan.FromSeconds(corsSettings.ExpirationSeconds);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
#if DEBUG
        options.Cookie.SameSite = SameSiteMode.Lax; // Not None!
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;
#else
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
#endif

        // THIS IS THE KEY PART - RETURN 401/403 INSTEAD OF REDIRECTING
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<GestionHogar.Utils.BearerSecuritySchemeTransformer>();
});

// Register modules
var modules = new IModule[]
{
    new AuthModule(),
    new ClientModule(),
    new LeadModule(),
    new LeadTaskModule(),
    new QuotationModule(),
};
foreach (var module in modules)
{
    module.SetupModule(builder.Services, builder.Configuration);
}

var app = builder.Build();

app.UseCors("AllowOrigins");
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Theme = ScalarTheme.Solarized;
        options.WithCustomCss(
            """
            @import url('https://fonts.googleapis.com/css2?family=Fira+Code:wght@300..700&family=Montserrat:ital,wght@0,100..900;1,100..900&display=swap');
            :root { --scalar-font: "Montserrat", sans-serif; --scalar-font-code: "Fira Code", monospace; }
            #v-0 {max-width: 100% !important}
            """
        );
    });
}

// Seed the database
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DatabaseSeeder.SeedRoles(app.Services, logger);
    await DatabaseSeeder.SeedDefaultUserAsync(app.Services, logger);

    // Apply more seeds when not in prod or staging
    if (!app.Environment.IsProduction() && !app.Environment.IsStaging())
    {
        logger.LogInformation("Seeding development data");
    }
}

app.UseGlobalExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseSecurityStampValidator();
app.MapControllers();

app.Run();
