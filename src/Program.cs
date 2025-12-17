using System.Text;
using GestionHogar.Configuration;
using GestionHogar.Controllers;
using GestionHogar.Controllers.ApiPeru;
using GestionHogar.Controllers.ExcelExport;
using GestionHogar.Controllers.Notifications;
using GestionHogar.Model;
using GestionHogar.Services;
using GestionHogar.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder
    .Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()
        );
        options.JsonSerializerOptions.ReferenceHandler = System
            .Text
            .Json
            .Serialization
            .ReferenceHandler
            .IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System
            .Text
            .Json
            .Serialization
            .JsonIgnoreCondition
            .WhenWritingNull;
    });

// Configure file upload limits
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50MB limit
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

//
// Configuration setup
//
builder.Services.Configure<CorsConfiguration>(builder.Configuration.GetSection("Cors"));
builder.Services.Configure<ApiPeruConfiguration>(builder.Configuration.GetSection("ApiPeru"));
builder.Services.Configure<CloudflareR2Configuration>(
    builder.Configuration.GetSection("CloudflareR2")
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
            var corsSettings = builder
                .Services.BuildServiceProvider()
                .GetRequiredService<IOptions<CorsConfiguration>>()
                .Value;
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
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSettings =
            builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
            ?? throw new Exception("JWT settings not found");
        var corsSettings = builder
            .Services.BuildServiceProvider()
            .GetRequiredService<IOptions<CorsConfiguration>>()
            .Value;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.SecretKey)
            ),
            ClockSkew = TimeSpan.Zero,
        };

        // Custom logic to read token from both Authorization header AND cookies
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // First check Authorization header (standard way)
                var token = context
                    .Request.Headers.Authorization.FirstOrDefault()
                    ?.Split(" ")
                    .Last();

                // If no Authorization header, check cookies
                if (string.IsNullOrEmpty(token))
                {
                    token = context.Request.Cookies[corsSettings.CookieName];
                }

                if (!string.IsNullOrEmpty(token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync("{\"error\":\"Unauthorized\"}");
            },
            OnForbidden = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync("{\"error\":\"Forbidden\"}");
            },
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
    new ProjectModule(),
    new BlockModule(),
    new LotModule(),
    new ReservationModule(),
    new ExchangeRateModule(),
    new PaymentModule(),
    new ApiPeruModule(),
    new PaymentTransactionModule(),
    new PaginationModule(),
    new ExcelExportModule(),
    new EmailModule(),
    new UserHigherRankModule(),
    new LandingModule(),
    new NotificationModule(),
};

// Register dashboard services
builder.Services.AddDashboardServices();
foreach (var module in modules)
{
    module.SetupModule(builder.Services, builder.Configuration);
}

// Extra services
builder.Services.AddScoped<WordTemplateService>();
builder.Services.AddScoped<SofficeConverterService>();
builder.Services.AddScoped<ICloudflareService, CloudflareService>();

// Background services
builder.Services.AddHostedService<LeadExpirationService>();

// Health checks
builder.Services.AddHealthChecks().AddCheck<LeadExpirationHealthCheck>("lead-expiration-service");

// Register controllers explicitly to avoid constructor conflicts
builder.Services.AddScoped<UsersController>();
builder.Services.AddScoped<AuthController>();

var app = builder.Build();

app.UseCors("AllowOrigins");
app.UseStaticFiles();

// Configure request size limits for file uploads
app.Use(
    async (context, next) =>
    {
        context
            .Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>()!
            .MaxRequestBodySize = 50 * 1024 * 1024; // 50MB
        await next();
    }
);
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

// Apply database migrations automatically with retry logic
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var context = services.GetRequiredService<DatabaseContext>();

    // Retry configuration
    const int maxRetries = 10;
    const int initialDelayMs = 2000; // Start with 2 seconds
    var currentDelay = initialDelayMs;

    // Log connection string (without password) for debugging
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(connectionString))
    {
        var safeConnectionString = connectionString.Contains("Password=")
            ? connectionString.Substring(0, connectionString.IndexOf("Password=")) + "Password=***"
            : connectionString;
        logger.LogInformation("📡 Connection string: {ConnectionString}", safeConnectionString);
    }

    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            logger.LogInformation(
                $"🔍 Checking database connection (attempt {attempt}/{maxRetries})..."
            );

            // Test connection
            var canConnect = context.Database.CanConnect();
            if (!canConnect)
            {
                throw new Exception("Cannot connect to database");
            }

            logger.LogInformation("✅ Database connection established");
            logger.LogInformation("🔍 Checking for pending migrations...");

            var pendingMigrations = context.Database.GetPendingMigrations();
            if (pendingMigrations.Any())
            {
                logger.LogInformation(
                    $"📦 Applying {pendingMigrations.Count()} pending migrations: {string.Join(", ", pendingMigrations)}"
                );
                context.Database.Migrate();
                logger.LogInformation("✅ Migrations applied successfully");
            }
            else
            {
                logger.LogInformation("✅ Database is up to date - no pending migrations");
            }

            // Success - break out of retry loop
            break;
        }
        catch (Exception ex)
        {
            if (attempt == maxRetries)
            {
                logger.LogError(
                    ex,
                    "❌ Failed to connect to database after {MaxRetries} attempts",
                    maxRetries
                );
                throw;
            }

            logger.LogWarning(
                ex,
                "⚠️  Database connection failed (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}ms...",
                attempt,
                maxRetries,
                currentDelay
            );

            Thread.Sleep(currentDelay);

            // Exponential backoff: double the delay for next attempt
            currentDelay = Math.Min(currentDelay * 2, 30000); // Max 30 seconds
        }
    }
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
        await DatabaseSeeder.SeedDevelopmentUsers(app.Services, logger);
    }
}

app.UseGlobalExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseSecurityStampValidator();
app.UseAuthenticationMiddleware();
app.MapControllers();
app.MapHealthChecks("/api/healthz");

// Libraries startup
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

app.Run();
