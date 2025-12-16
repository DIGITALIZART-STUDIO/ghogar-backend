using GestionHogar.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GestionHogar.Controllers;

public class LotModule : IModule
{
    public IServiceCollection SetupModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ILotService, LotService>();
        return services;
    }
}
