using GestionHogar.Services;

namespace GestionHogar.Controllers;

public class ContractModule : IModule
{
    public IServiceCollection SetupModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IContractService, ContractService>();
        services.AddScoped<IContractBackfillService, ContractBackfillService>();
        return services;
    }
}
