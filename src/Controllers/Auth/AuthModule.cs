namespace GestionHogar.Controllers;

public class AuthModule : IModule
{
    public IServiceCollection SetupModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<JwtService>();

        // Loads JWT secrets from appsettings into a JwtSettings class,
        // and makes it available to the program
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));

        return services;
    }
}
