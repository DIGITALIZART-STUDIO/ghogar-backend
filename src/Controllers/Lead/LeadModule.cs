using GestionHogar.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GestionHogar.Controllers;

public class LeadModule : IModule
{
    public IServiceCollection SetupModule(IServiceCollection services, IConfiguration configuration)
    {
        // Registrar el servicio
        services.AddScoped<ILeadService, LeadService>();

        return services;
    }
}
