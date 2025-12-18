using Microsoft.Extensions.DependencyInjection;

namespace GestionHogar.Controllers;

public static class DashboardModule
{
    public static IServiceCollection AddDashboardServices(this IServiceCollection services)
    {
        services.AddScoped<GetDashboardAdminDataUseCase>();
        services.AddScoped<GetAdvisorDashboardDataUseCase>();
        services.AddScoped<GetFinanceManagerDashboardDataUseCase>();
        services.AddScoped<GetSupervisorDashboardDataUseCase>();
        services.AddScoped<GetManagerDashboardDataUseCase>();
        services.AddScoped<DashboardController>();

        return services;
    }
}
{
    public static IServiceCollection AddDashboardServices(this IServiceCollection services)
    {
        services.AddScoped<GetDashboardAdminDataUseCase>();
        services.AddScoped<GetAdvisorDashboardDataUseCase>();
        services.AddScoped<GetFinanceManagerDashboardDataUseCase>();
        services.AddScoped<GetSupervisorDashboardDataUseCase>();
        services.AddScoped<GetManagerDashboardDataUseCase>();
        services.AddScoped<GetCommercialManagerDashboardDataUseCase>();
        services.AddScoped<DashboardController>();

        return services;
    }
}
