using GestionHogar.Services;

namespace GestionHogar.Controllers;

public class ReservationModule : IModule
{
    public IServiceCollection SetupModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IReservationService, ReservationService>();
        services.AddScoped<OdsTemplateService>();
        return services;
    }
}
