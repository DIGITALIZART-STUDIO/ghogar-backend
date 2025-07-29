using GestionHogar.Services;

namespace GestionHogar.Controllers;

public class DashboardModule : IModule
{
    public IServiceCollection SetupModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<DashboardService>();
        return services;
    }
}
