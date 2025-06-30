using GestionHogar.Services;

namespace GestionHogar.Controllers;

public class PaymentModule : IModule
{
    public IServiceCollection SetupModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPaymentService, PaymentService>();
        return services;
    }
}
