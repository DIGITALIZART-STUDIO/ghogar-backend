using GestionHogar.Services;

namespace GestionHogar.Controllers;

public class ClientModule : IModule
{
    public IServiceCollection SetupModule(IServiceCollection services, IConfiguration configuration)
    {
        // Registrar el servicio
        services.AddScoped<IClientService, ClientService>();

        return services;
    }
}
