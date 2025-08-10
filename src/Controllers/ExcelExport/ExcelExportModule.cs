using GestionHogar.Services;

namespace GestionHogar.Controllers.ExcelExport;

public class ExcelExportModule : IModule
{
    public IServiceCollection SetupModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IExcelExportService, ExcelExportService>();
        return services;
    }
}
