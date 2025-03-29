namespace GestionHogar.Controllers;

interface IModule
{
    IServiceCollection SetupModule(IServiceCollection services, IConfiguration configuration);
}
