using System.Text.Json;
using GestionHogar.Model;
using Microsoft.Extensions.Caching.Memory;

namespace GestionHogar.Services;

/// <summary>
/// Servicio de caché inteligente para metadatos de paginación
/// </summary>
public class PaginationCacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<PaginationCacheService>? _logger;

    // Configuración de caché
    private readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _countExpiration = TimeSpan.FromMinutes(10);
    private readonly TimeSpan _metadataExpiration = TimeSpan.FromMinutes(15);

    public PaginationCacheService(
        IMemoryCache cache,
        ILogger<PaginationCacheService>? logger = null
    )
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene conteo desde caché o lo calcula y guarda
    /// </summary>
    public async Task<int> GetOrSetCountAsync<T>(
        string cacheKey,
        Func<Task<int>> countFactory,
        TimeSpan? expiration = null
    )
    {
        if (_cache.TryGetValue(cacheKey, out int cachedCount))
        {
            _logger?.LogDebug(
                "Conteo obtenido del caché: {CacheKey} = {Count}",
                cacheKey,
                cachedCount
            );
            return cachedCount;
        }

        _logger?.LogDebug("Calculando conteo y guardando en caché: {CacheKey}", cacheKey);

        var count = await countFactory();

        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? _countExpiration,
            SlidingExpiration = TimeSpan.FromMinutes(2),
            Priority = CacheItemPriority.Normal,
            Size = 1, // Especificar tamaño para el cache
        };

        _cache.Set(cacheKey, count, cacheOptions);

        return count;
    }

    /// <summary>
    /// Obtiene metadatos de paginación desde caché o los calcula
    /// </summary>
    public async Task<PaginationMetadata> GetOrSetMetadataAsync(
        string cacheKey,
        Func<Task<PaginationMetadata>> metadataFactory,
        TimeSpan? expiration = null
    )
    {
        if (_cache.TryGetValue(cacheKey, out PaginationMetadata? cachedMetadata))
        {
            _logger?.LogDebug("Metadatos obtenidos del caché: {CacheKey}", cacheKey);
            return cachedMetadata!;
        }

        _logger?.LogDebug("Calculando metadatos y guardando en caché: {CacheKey}", cacheKey);

        var metadata = await metadataFactory();

        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? _metadataExpiration,
            SlidingExpiration = TimeSpan.FromMinutes(3),
            Priority = CacheItemPriority.High,
            Size = 1, // Especificar tamaño para el cache
        };

        _cache.Set(cacheKey, metadata, cacheOptions);

        return metadata;
    }

    /// <summary>
    /// Obtiene datos paginados desde caché o los calcula
    /// </summary>
    public async Task<PaginatedResponseV2<T>> GetOrSetPaginatedDataAsync<T>(
        string cacheKey,
        Func<Task<PaginatedResponseV2<T>>> dataFactory,
        TimeSpan? expiration = null
    )
    {
        if (_cache.TryGetValue(cacheKey, out PaginatedResponseV2<T>? cachedData))
        {
            _logger?.LogDebug("Datos paginados obtenidos del caché: {CacheKey}", cacheKey);
            return cachedData!;
        }

        _logger?.LogDebug("Calculando datos paginados y guardando en caché: {CacheKey}", cacheKey);

        var data = await dataFactory();

        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? _defaultExpiration,
            SlidingExpiration = TimeSpan.FromMinutes(1),
            Priority = CacheItemPriority.Low,
            Size = 1, // Especificar tamaño para el cache
        };

        _cache.Set(cacheKey, data, cacheOptions);

        return data;
    }

    /// <summary>
    /// Invalida caché por patrón
    /// </summary>
    public void InvalidateByPattern(string pattern)
    {
        _logger?.LogInformation("Invalidando caché por patrón: {Pattern}", pattern);

        // En una implementación real, esto requeriría un caché distribuido
        // o un mecanismo de invalidación más sofisticado
        // Por ahora, solo logueamos la invalidación
    }

    /// <summary>
    /// Invalida caché específico
    /// </summary>
    public void Invalidate(string cacheKey)
    {
        _cache.Remove(cacheKey);
        _logger?.LogDebug("Caché invalidado: {CacheKey}", cacheKey);
    }

    /// <summary>
    /// Invalida múltiples claves de caché
    /// </summary>
    public void InvalidateMany(IEnumerable<string> cacheKeys)
    {
        foreach (var key in cacheKeys)
        {
            _cache.Remove(key);
        }

        _logger?.LogDebug("Invalidadas {Count} claves de caché", cacheKeys.Count());
    }

    /// <summary>
    /// Genera clave de caché para conteo
    /// </summary>
    public string GenerateCountCacheKey<T>(IQueryable<T> query, string? filterHash = null)
    {
        var queryString = query.ToString();
        var hash = GenerateHash(queryString + (filterHash ?? ""));
        return $"pagination:count:{typeof(T).Name}:{hash}";
    }

    /// <summary>
    /// Genera clave de caché para metadatos
    /// </summary>
    public string GenerateMetadataCacheKey<T>(
        IQueryable<T> query,
        int page,
        int pageSize,
        string? filterHash = null
    )
    {
        var queryString = query.ToString();
        var hash = GenerateHash(queryString + (filterHash ?? ""));
        return $"pagination:metadata:{typeof(T).Name}:{hash}:{page}:{pageSize}";
    }

    /// <summary>
    /// Genera clave de caché para datos paginados
    /// </summary>
    public string GenerateDataCacheKey<T>(
        IQueryable<T> query,
        int page,
        int pageSize,
        string? filterHash = null
    )
    {
        var queryString = query.ToString();
        var hash = GenerateHash(queryString + (filterHash ?? ""));
        return $"pagination:data:{typeof(T).Name}:{hash}:{page}:{pageSize}";
    }

    /// <summary>
    /// Genera hash de consulta
    /// </summary>
    private string GenerateHash(string input)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(input)
        );
        return Convert.ToHexString(hash)[..16];
    }

    /// <summary>
    /// Obtiene estadísticas del caché
    /// </summary>
    public CacheStatistics GetCacheStatistics()
    {
        // En una implementación real, esto obtendría estadísticas reales del caché
        return new CacheStatistics
        {
            TotalEntries = 0, // Placeholder
            HitRate = 0.0, // Placeholder
            MemoryUsage = 0, // Placeholder
        };
    }

    /// <summary>
    /// Limpia caché expirado
    /// </summary>
    public void CleanupExpiredCache()
    {
        _logger?.LogDebug("Limpiando caché expirado");

        // En una implementación real, esto limpiaría entradas expiradas
        // IMemoryCache maneja esto automáticamente, pero podríamos
        // implementar lógica adicional aquí
    }

    /// <summary>
    /// Configura TTL dinámico basado en el tipo de datos
    /// </summary>
    public TimeSpan GetDynamicTTL<T>(string dataType)
    {
        return dataType.ToLower() switch
        {
            "count" => _countExpiration,
            "metadata" => _metadataExpiration,
            "data" => _defaultExpiration,
            "static" => TimeSpan.FromHours(1),
            "dynamic" => TimeSpan.FromMinutes(2),
            _ => _defaultExpiration,
        };
    }
}

/// <summary>
/// Estadísticas del caché
/// </summary>
public class CacheStatistics
{
    public int TotalEntries { get; set; }
    public double HitRate { get; set; }
    public long MemoryUsage { get; set; }
    public int ExpiredEntries { get; set; }
    public DateTime LastCleanup { get; set; }
}

/// <summary>
/// Configuración de caché para paginación
/// </summary>
public class PaginationCacheConfiguration
{
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan CountExpiration { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan MetadataExpiration { get; set; } = TimeSpan.FromMinutes(15);
    public int MaxCacheSize { get; set; } = 1000;
    public bool EnableCache { get; set; } = true;
    public bool EnableCountCache { get; set; } = true;
    public bool EnableMetadataCache { get; set; } = true;
    public bool EnableDataCache { get; set; } = false; // Por defecto deshabilitado para datos
}
