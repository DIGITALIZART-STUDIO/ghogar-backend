using GestionHogar.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GestionHogar.Controllers;

public class ReportsModule : IModule
{
    public IServiceCollection SetupModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICommercialReportService, CommercialReportService>();
        services.AddScoped<IAdministrativeReportService, AdministrativeReportService>();

        return services;
    }
}
