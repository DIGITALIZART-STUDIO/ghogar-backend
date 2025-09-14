using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace GestionHogar.Controllers;

/// <summary>
/// Middleware para caché de respuestas de paginación
/// </summary>
public class PaginationResponseCachingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PaginationResponseCachingMiddleware> _logger;

    // Configuración de caché
    private readonly TimeSpan _defaultCacheExpiration = TimeSpan.FromMinutes(5);
    private readonly int _maxCacheSize = 1000;

    public PaginationResponseCachingMiddleware(
        RequestDelegate next,
        IMemoryCache cache,
        ILogger<PaginationResponseCachingMiddleware> logger
    )
    {
        _next = next;
        _cache = cache;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Solo aplicar caché a endpoints de paginación
        if (!IsPaginationEndpoint(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Solo cachear respuestas GET y POST exitosas
        if (!ShouldCacheRequest(context.Request))
        {
            await _next(context);
            return;
        }

        // Generar clave de caché
        var cacheKey = GenerateCacheKey(context.Request);

        // Intentar obtener respuesta del caché
        if (_cache.TryGetValue(cacheKey, out CachedResponse? cachedResponse))
        {
            _logger.LogDebug("Respuesta obtenida del caché: {CacheKey}", cacheKey);

            // Escribir respuesta desde caché
            await WriteCachedResponse(context, cachedResponse);
            return;
        }

        // Capturar respuesta original
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await _next(context);

        // Solo cachear respuestas exitosas
        if (context.Response.StatusCode == 200)
        {
            var response = await CaptureResponse(context, responseBody);

            // Guardar en caché
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = GetCacheExpiration(context.Request),
                SlidingExpiration = TimeSpan.FromMinutes(2),
                Priority = CacheItemPriority.Normal,
                Size = CalculateResponseSize(response),
            };

            _cache.Set(cacheKey, response, cacheOptions);

            _logger.LogDebug("Respuesta guardada en caché: {CacheKey}", cacheKey);
        }

        // Escribir respuesta al cliente
        responseBody.Seek(0, SeekOrigin.Begin);
        await responseBody.CopyToAsync(originalBodyStream);
    }

    /// <summary>
    /// Determina si el endpoint es de paginación
    /// </summary>
    private static bool IsPaginationEndpoint(PathString path)
    {
        var pathValue = path.Value?.ToLower() ?? "";
        return pathValue.Contains("/paginated")
            || pathValue.Contains("/pagination")
            || pathValue.Contains("/paginate");
    }

    /// <summary>
    /// Determina si la solicitud debe ser cacheada
    /// </summary>
    private static bool ShouldCacheRequest(HttpRequest request)
    {
        // Solo cachear GET y POST
        if (request.Method != "GET" && request.Method != "POST")
        {
            return false;
        }

        // No cachear si hay headers de no-cache
        if (
            request.Headers.ContainsKey("Cache-Control")
            && request.Headers["Cache-Control"].ToString().Contains("no-cache")
        )
        {
            return false;
        }

        // No cachear si hay headers de autorización especiales
        if (
            request.Headers.ContainsKey("Authorization")
            && request.Headers["Authorization"].ToString().Contains("Bearer")
        )
        {
            // Solo cachear si es un usuario específico (implementación simplificada)
            return true;
        }

        return true;
    }

    /// <summary>
    /// Genera clave de caché basada en la solicitud
    /// </summary>
    private string GenerateCacheKey(HttpRequest request)
    {
        var keyParts = new List<string>
        {
            "pagination_cache",
            request.Method,
            request.Path.Value ?? "",
            request.QueryString.Value ?? "",
        };

        // Incluir headers relevantes
        if (request.Headers.ContainsKey("Authorization"))
        {
            var authHeader = request.Headers["Authorization"].ToString();
            // Usar solo una parte del token para la clave (por seguridad)
            keyParts.Add(authHeader[..Math.Min(20, authHeader.Length)]);
        }

        // Incluir body para POST requests
        if (request.Method == "POST" && request.ContentLength > 0)
        {
            // En una implementación real, leeríamos el body aquí
            keyParts.Add("post_body");
        }

        var key = string.Join(":", keyParts);
        return Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key))
        )[..32];
    }

    /// <summary>
    /// Captura la respuesta para guardarla en caché
    /// </summary>
    private async Task<CachedResponse> CaptureResponse(
        HttpContext context,
        MemoryStream responseBody
    )
    {
        responseBody.Seek(0, SeekOrigin.Begin);
        var responseText = await new StreamReader(responseBody).ReadToEndAsync();

        return new CachedResponse
        {
            StatusCode = context.Response.StatusCode,
            Headers = context.Response.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
            Body = responseText,
            ContentType = context.Response.ContentType ?? "application/json",
        };
    }

    /// <summary>
    /// Escribe respuesta desde caché
    /// </summary>
    private async Task WriteCachedResponse(HttpContext context, CachedResponse cachedResponse)
    {
        context.Response.StatusCode = cachedResponse.StatusCode;
        context.Response.ContentType = cachedResponse.ContentType;

        // Escribir headers
        foreach (var header in cachedResponse.Headers)
        {
            context.Response.Headers[header.Key] = header.Value;
        }

        // Agregar header de caché
        context.Response.Headers["X-Cache"] = "HIT";
        context.Response.Headers["X-Cache-Timestamp"] = DateTime.UtcNow.ToString("O");

        // Escribir body
        await context.Response.WriteAsync(cachedResponse.Body);
    }

    /// <summary>
    /// Obtiene tiempo de expiración del caché basado en la solicitud
    /// </summary>
    private TimeSpan GetCacheExpiration(HttpRequest request)
    {
        // TTL dinámico basado en el tipo de endpoint
        var path = request.Path.Value?.ToLower() ?? "";

        return path switch
        {
            var p when p.Contains("performance-stats") => TimeSpan.FromMinutes(15),
            var p when p.Contains("index-recommendations") => TimeSpan.FromHours(1),
            var p when p.Contains("paginated") => TimeSpan.FromMinutes(5),
            _ => _defaultCacheExpiration,
        };
    }

    /// <summary>
    /// Calcula tamaño de la respuesta para el caché
    /// </summary>
    private long CalculateResponseSize(CachedResponse response)
    {
        var size = response.Body.Length;

        // Agregar tamaño de headers
        foreach (var header in response.Headers)
        {
            size += header.Key.Length + header.Value.Length;
        }

        return size;
    }
}

/// <summary>
/// Respuesta cacheada
/// </summary>
public class CachedResponse
{
    public int StatusCode { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public string Body { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Configuración del middleware de caché
/// </summary
public class PaginationResponseCachingOptions
{
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxCacheSize { get; set; } = 1000;
    public bool EnableCache { get; set; } = true;
    public string[] CacheableEndpoints { get; set; } =
        new[] { "/paginated", "/pagination", "/paginate" };
    public string[] ExcludedHeaders { get; set; } =
        new[] { "Authorization", "Cookie", "X-Forwarded-For" };
}

/// <summary>
/// Extensión para registrar el middleware
/// </summary>
public static class PaginationResponseCachingMiddlewareExtensions
{
    public static IApplicationBuilder UsePaginationResponseCaching(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<PaginationResponseCachingMiddleware>();
    }

    public static IApplicationBuilder UsePaginationResponseCaching(
        this IApplicationBuilder builder,
        Action<PaginationResponseCachingOptions> configureOptions
    )
    {
        var options = new PaginationResponseCachingOptions();
        configureOptions(options);

        return builder.UseMiddleware<PaginationResponseCachingMiddleware>();
    }
}
