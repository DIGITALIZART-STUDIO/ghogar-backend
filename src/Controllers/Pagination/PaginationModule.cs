using GestionHogar.Services;

namespace GestionHogar.Controllers;

public class PaginationModule : IModule
{
    public IServiceCollection SetupModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<PaginationService>();
        return services;
    }
}
