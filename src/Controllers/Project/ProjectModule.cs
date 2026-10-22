using GestionHogar.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GestionHogar.Controllers;

public class ProjectModule : IModule
{
    public IServiceCollection SetupModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ContractHtmlService>();
        services.AddScoped<IProjectContractService, ProjectContractService>();
        return services;
    }
}
