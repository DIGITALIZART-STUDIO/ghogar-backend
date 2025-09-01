using Microsoft.Extensions.DependencyInjection;

namespace GestionHogar.Controllers;

public static class DashboardModule
{
    public static IServiceCollection AddDashboardServices(this IServiceCollection services)
    {
        services.AddScoped<GetDashboardAdminDataUseCase>();
        services.AddScoped<GetAdvisorDashboardDataUseCase>();
        services.AddScoped<DashboardController>();

        return services;
    }
}
