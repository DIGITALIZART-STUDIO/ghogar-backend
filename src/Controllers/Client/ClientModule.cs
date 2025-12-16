using GestionHogar.Services;

namespace GestionHogar.Controllers;

public class ClientModule : IModule
{
    public IServiceCollection SetupModule(IServiceCollection services, IConfiguration configuration)
    {
        // Registrar los servicios
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IExcelTemplateService, ExcelTemplateService>();

        return services;
    }
}
