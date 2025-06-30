using GestionHogar.Services;

namespace GestionHogar.Controllers.ApiPeru;

public class ApiPeruModule : IModule
{
    public IServiceCollection SetupModule(IServiceCollection services, IConfiguration configuration)
    {
        // Registrar el servicio con HttpClient
        services.AddHttpClient<ApiPeruService>();
        return services;
    }
}
