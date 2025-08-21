using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GestionHogar.Controllers;

public class UserHigherRankModule : IModule
{
    public IServiceCollection SetupModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IUserHigherRankService, UserHigherRankService>();
        return services;
    }
}
