using GestionHogar.Controllers;
using GestionHogar.Services;

namespace GestionHogar.Controllers.Notifications;

public class NotificationModule : IModule
{
    public IServiceCollection SetupModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }
}
