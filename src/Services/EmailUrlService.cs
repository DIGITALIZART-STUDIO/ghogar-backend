using GestionHogar.Configuration;
using Microsoft.Extensions.Options;

namespace GestionHogar.Services;

public interface IEmailUrlService
{
    string GetAbsoluteUrl(string relativeUrl);
    string GetLogoUrl();
    string GetWebsiteUrl();
}

public class EmailUrlService : IEmailUrlService
{
    private readonly BusinessInfo _businessInfo;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;

    public EmailUrlService(
        IOptions<BusinessInfo> businessInfo,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration
    )
    {
        _businessInfo = businessInfo.Value;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
    }

    public string GetAbsoluteUrl(string relativeUrl)
    {
        if (string.IsNullOrEmpty(relativeUrl))
            return string.Empty;

        // Si ya es una URL absoluta, devolverla tal como está
        if (relativeUrl.StartsWith("http://") || relativeUrl.StartsWith("https://"))
            return relativeUrl;

        // Obtener la URL base del servidor
        var baseUrl = GetBaseUrl();

        // Combinar la URL base con la ruta relativa
        return $"{baseUrl.TrimEnd('/')}/{relativeUrl.TrimStart('/')}";
    }

    public string GetLogoUrl()
    {
        // La URL del logo ya es absoluta, devolverla directamente
        return _businessInfo.LogoUrl;
    }

    public string GetWebsiteUrl()
    {
        return _businessInfo.Url;
    }

    private string GetBaseUrl()
    {
        // Intentar obtener la URL base del contexto HTTP actual
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            var request = httpContext.Request;
            return $"{request.Scheme}://{request.Host}";
        }

        // Si no hay contexto HTTP (por ejemplo, en background services),
        // usar la configuración o valores por defecto
        var environment = _configuration["ASPNETCORE_ENVIRONMENT"];

        if (environment == "Development")
        {
            return "http://localhost:5165"; // Puerto por defecto de desarrollo
        }
        else if (environment == "Production")
        {
            // En producción, usar el dominio configurado
            return "https://gestionhogar-backend-develop.araozu.dev";
        }
        else
        {
            // Para otros entornos, usar el dominio de desarrollo
            return "https://gestionhogar-backend-develop.araozu.dev";
        }
    }
}
