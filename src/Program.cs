using System.Text;
using GestionHogar.Controllers;
using GestionHogar.Model;
using GestionHogar.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddControllers();

// Database setup
builder.Services.AddDbContext<DatabaseContext>(options =>
{
    var connectionString =
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new Exception("DB connection string not found");
    options.UseNpgsql(connectionString);
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

// Add JWT authentication
builder
    .Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            // Clock skew compensates for server time drift
            ClockSkew = TimeSpan.Zero,
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    builder.Configuration["Jwt:SecretKey"]
                        ?? throw new Exception("Jwt:SecretKey not set")
                )
            ),
            RequireSignedTokens = true,
            RequireExpirationTime = true,
        };

#if DEBUG
        // During development, use the cookie set by the frontend
        // as JWT token, for not having to manually login and set
        // Bearer on every change.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                context.Token = context.Request.Cookies["pc_access_token"];
                return Task.CompletedTask;
            },
        };
#endif
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
};
foreach (var module in modules)
{
    module.SetupModule(builder.Services, builder.Configuration);
}

var app = builder.Build();

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
