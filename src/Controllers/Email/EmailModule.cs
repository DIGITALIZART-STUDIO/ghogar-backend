using GestionHogar.Configuration;
using GestionHogar.Services;

namespace GestionHogar.Controllers;

public class EmailModule : IModule
{
    public IServiceCollection SetupModule(IServiceCollection services, IConfiguration configuration)
    {
        // Configurar las opciones de email
        services.Configure<EmailConfiguration>(configuration.GetSection("Email"));
        services.Configure<BusinessInfo>(configuration.GetSection("BusinessInfo"));

        // Registrar los servicios de email
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IEmailTemplateService, EmailTemplateService>();
        services.AddScoped<IEmailUrlService, EmailUrlService>();
        services.AddHttpContextAccessor();

        return services;
    }
}
