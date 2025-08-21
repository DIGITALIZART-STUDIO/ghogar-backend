using GestionHogar.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GestionHogar.Controllers;

public class QuotationModule : IModule
{
    public IServiceCollection SetupModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IQuotationService, QuotationService>();
        services.AddScoped<ILeadService, LeadService>();
        return services; // Devolver services para coincidir con la interfaz
    }
}
