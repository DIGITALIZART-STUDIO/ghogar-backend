using System.Diagnostics;
using System.Linq.Expressions;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GestionHogar.Services;

/// <summary>
/// Servicio avanzado de paginación con optimizaciones para grandes volúmenes de datos
/// </summary>
public class AdvancedPaginationService
{
    private readonly ILogger<AdvancedPaginationService>? _logger;
    private readonly IMemoryCache? _cache;

    // Configuración de optimizaciones
    private const int LargeOffsetThreshold = 10000; // Umbral para considerar offset grande
    private const int MaxPageSize = 1000; // Tamaño máximo de página
    private const int CacheExpirationMinutes = 5; // TTL del caché en minutos

    public AdvancedPaginationService(
        ILogger<AdvancedPaginationService>? logger = null,
        IMemoryCache? cache = null
    )
    {
        _logger = logger;
        _cache = cache;
    }

    /// <summary>
    /// Pagina una consulta con optimizaciones avanzadas
    /// </summary>
    public async Task<PaginatedResponseV2<T>> PaginateAdvancedAsync<T>(
        IQueryable<T> query,
        int page,
        int pageSize,
        string? orderBy = null,
        bool useCursorPagination = false,
        string? cursor = null,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Validar parámetros
            var validatedParams = ValidateAdvancedPaginationParams(page, pageSize);
            page = validatedParams.Page;
            pageSize = validatedParams.PageSize;

            // Determinar estrategia de paginación
            var offset = (page - 1) * pageSize;
            var useOptimizedStrategy = offset > LargeOffsetThreshold || useCursorPagination;

            _logger?.LogDebug(
                "Iniciando paginación avanzada: página {Page}, pageSize {PageSize}, offset {Offset}, estrategia optimizada: {UseOptimized}",
                page,
                pageSize,
                offset,
                useOptimizedStrategy
            );

            PaginatedResponseV2<T> result;

            if (useOptimizedStrategy && useCursorPagination)
            {
                result = await ExecuteCursorBasedPagination(
                    query,
                    pageSize,
                    cursor,
                    orderBy,
                    cancellationToken
                );
            }
            else if (useOptimizedStrategy)
            {
                result = await ExecuteOptimizedOffsetPagination(
                    query,
                    page,
                    pageSize,
                    orderBy,
                    cancellationToken
                );
            }
            else
            {
                result = await ExecuteStandardPagination(
                    query,
                    page,
                    pageSize,
                    orderBy,
                    cancellationToken
                );
            }

            stopwatch.Stop();
            result.Meta.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            _logger?.LogDebug(
                "Paginación avanzada completada: {Total} elementos, tiempo: {ExecutionTime}ms",
                result.Meta.Total,
                stopwatch.ElapsedMilliseconds
            );

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogError(
                ex,
                "Error en paginación avanzada: página {Page}, pageSize {PageSize}, tiempo: {ExecutionTime}ms",
                page,
                pageSize,
                stopwatch.ElapsedMilliseconds
            );
            throw;
        }
    }

    /// <summary>
    /// Ejecuta paginación estándar para offsets pequeños
    /// </summary>
    private async Task<PaginatedResponseV2<T>> ExecuteStandardPagination<T>(
        IQueryable<T> query,
        int page,
        int pageSize,
        string? orderBy,
        CancellationToken cancellationToken
    )
    {
        // Aplicar ordenamiento si se especifica
        if (!string.IsNullOrEmpty(orderBy))
        {
            query = ApplyOrdering(query, orderBy);
        }

        // Ejecutar secuencialmente para evitar problemas de concurrencia con DbContext
        var total = await GetOptimizedCountAsync(query, cancellationToken);
        var data = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return PaginatedResponseV2<T>.Create(data, total, page, pageSize);
    }

    /// <summary>
    /// Ejecuta paginación optimizada para offsets grandes
    /// </summary>
    private async Task<PaginatedResponseV2<T>> ExecuteOptimizedOffsetPagination<T>(
        IQueryable<T> query,
        int page,
        int pageSize,
        string? orderBy,
        CancellationToken cancellationToken
    )
    {
        // Para offsets grandes, usar estrategia de "seek method"
        if (!string.IsNullOrEmpty(orderBy))
        {
            query = ApplyOrdering(query, orderBy);
        }

        // Usar ID-based pagination si es posible
        if (typeof(T).GetProperty("Id") != null)
        {
            return await ExecuteIdBasedPagination(query, page, pageSize, cancellationToken);
        }

        // Fallback a paginación estándar con optimizaciones
        var total = await GetOptimizedCountAsync(query, cancellationToken);
        var data = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return PaginatedResponseV2<T>.Create(data, total, page, pageSize);
    }

    /// <summary>
    /// Ejecuta paginación basada en cursor para mejor performance
    /// </summary>
    private async Task<PaginatedResponseV2<T>> ExecuteCursorBasedPagination<T>(
        IQueryable<T> query,
        int pageSize,
        string? cursor,
        string? orderBy,
        CancellationToken cancellationToken
    )
    {
        // Implementación básica de cursor-based pagination
        // En una implementación completa, esto usaría un cursor real

        if (!string.IsNullOrEmpty(orderBy))
        {
            query = ApplyOrdering(query, orderBy);
        }

        // Para simplicidad, usar paginación estándar con cursor simulado
        var total = await GetOptimizedCountAsync(query, cancellationToken);
        var data = await query.Take(pageSize).ToListAsync(cancellationToken);

        return PaginatedResponseV2<T>.Create(data, total, 1, pageSize);
    }

    /// <summary>
    /// Ejecuta paginación basada en ID para mejor performance con offsets grandes
    /// </summary>
    private async Task<PaginatedResponseV2<T>> ExecuteIdBasedPagination<T>(
        IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        // Obtener el ID mínimo para esta página
        var minId = await query
            .OrderBy(x => EF.Property<Guid>(x, "Id"))
            .Skip((page - 1) * pageSize)
            .Select(x => EF.Property<Guid>(x, "Id"))
            .FirstOrDefaultAsync(cancellationToken);

        if (minId == Guid.Empty)
        {
            return PaginatedResponseV2<T>.Empty(page, pageSize);
        }

        // Obtener datos usando ID como filtro
        var data = await query
            .Where(x => EF.Property<Guid>(x, "Id") >= minId)
            .OrderBy(x => EF.Property<Guid>(x, "Id"))
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Obtener conteo total (con caché si está disponible)
        var total = await GetOptimizedCountAsync(query, cancellationToken);

        return PaginatedResponseV2<T>.Create(data, total, page, pageSize);
    }

    /// <summary>
    /// Obtiene conteo optimizado con caché
    /// </summary>
    private async Task<int> GetOptimizedCountAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken
    )
    {
        // Generar clave de caché basada en la consulta
        var cacheKey = GenerateCacheKey(query);

        if (_cache != null && _cache.TryGetValue(cacheKey, out int cachedCount))
        {
            _logger?.LogDebug("Conteo obtenido del caché: {Count}", cachedCount);
            return cachedCount;
        }

        // Ejecutar conteo optimizado
        var count = await query.CountAsync(cancellationToken);

        // Guardar en caché si está disponible
        if (_cache != null)
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheExpirationMinutes),
                SlidingExpiration = TimeSpan.FromMinutes(2),
                Size = 1, // Especificar tamaño para el cache
            };
            _cache.Set(cacheKey, count, cacheOptions);
        }

        _logger?.LogDebug("Conteo calculado y guardado en caché: {Count}", count);
        return count;
    }

    /// <summary>
    /// Aplica ordenamiento a la consulta
    /// </summary>
    private IQueryable<T> ApplyOrdering<T>(IQueryable<T> query, string orderBy)
    {
        // Implementación básica de ordenamiento dinámico
        // En una implementación completa, esto sería más robusto

        var orderParts = orderBy.Split(' ');
        var propertyName = orderParts[0];
        var direction = orderParts.Length > 1 ? orderParts[1] : "asc";

        try
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var property = Expression.Property(parameter, propertyName);
            var lambda = Expression.Lambda(property, parameter);

            var methodName = direction.ToLower() == "desc" ? "OrderByDescending" : "OrderBy";
            var method = typeof(Queryable)
                .GetMethods()
                .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(T), property.Type);

            return (IQueryable<T>)method.Invoke(null, new object[] { query, lambda })!;
        }
        catch
        {
            // Fallback a ordenamiento por ID si falla
            return query.OrderBy(x => EF.Property<Guid>(x, "Id"));
        }
    }

    /// <summary>
    /// Genera clave de caché para la consulta
    /// </summary>
    private string GenerateCacheKey<T>(IQueryable<T> query)
    {
        var queryString = query.ToString();
        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(queryString)
            )
        );
        return $"pagination_count_{hash[..16]}";
    }

    /// <summary>
    /// Valida parámetros de paginación avanzada
    /// </summary>
    private static PaginationParams ValidateAdvancedPaginationParams(int page, int pageSize)
    {
        // Validaciones más estrictas para paginación avanzada
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        return PaginationParams.Create(page, pageSize);
    }

    /// <summary>
    /// Obtiene estadísticas de performance de paginación
    /// </summary>
    public async Task<PaginationPerformanceStats> GetPerformanceStatsAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();

        var totalCount = await GetOptimizedCountAsync(query, cancellationToken);
        var queryString = query.ToString();

        stopwatch.Stop();

        return new PaginationPerformanceStats
        {
            TotalRecords = totalCount,
            QueryComplexity = CalculateQueryComplexity(queryString),
            EstimatedPageCount = (int)Math.Ceiling((double)totalCount / 10), // Asumiendo pageSize 10
            CountExecutionTimeMs = stopwatch.ElapsedMilliseconds,
            RecommendedPageSize = CalculateRecommendedPageSize(totalCount),
            HasIndexes = await CheckForRecommendedIndexes(query, cancellationToken),
        };
    }

    /// <summary>
    /// Calcula complejidad de la consulta
    /// </summary>
    private int CalculateQueryComplexity(string queryString)
    {
        var complexity = 1;

        if (queryString.Contains("JOIN"))
            complexity += 2;
        if (queryString.Contains("WHERE"))
            complexity += 1;
        if (queryString.Contains("ORDER BY"))
            complexity += 1;
        if (queryString.Contains("GROUP BY"))
            complexity += 2;
        if (queryString.Contains("HAVING"))
            complexity += 1;

        return complexity;
    }

    /// <summary>
    /// Calcula tamaño de página recomendado
    /// </summary>
    private int CalculateRecommendedPageSize(int totalCount)
    {
        return totalCount switch
        {
            < 100 => 10,
            < 1000 => 25,
            < 10000 => 50,
            _ => 100,
        };
    }

    /// <summary>
    /// Verifica si existen índices recomendados
    /// </summary>
    private async Task<bool> CheckForRecommendedIndexes<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken
    )
    {
        // Implementación básica - en producción esto sería más sofisticado
        var queryString = query.ToString();
        return !queryString.Contains("WHERE") || queryString.Contains("Id");
    }
}

/// <summary>
/// Estadísticas de performance de paginación
/// </summary>
public class PaginationPerformanceStats
{
    public int TotalRecords { get; set; }
    public int QueryComplexity { get; set; }
    public int EstimatedPageCount { get; set; }
    public long CountExecutionTimeMs { get; set; }
    public int RecommendedPageSize { get; set; }
    public bool HasIndexes { get; set; }
}
