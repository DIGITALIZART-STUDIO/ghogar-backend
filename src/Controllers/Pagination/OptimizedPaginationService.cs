using System.Diagnostics;
using System.Linq.Expressions;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GestionHogar.Services;

/// <summary>
/// Servicio de paginación optimizado con lógica de negocio mejorada y performance optimizada
/// </summary>
public class OptimizedPaginationService
{
    private readonly ILogger<OptimizedPaginationService>? _logger;
    private readonly IMemoryCache? _cache;
    private readonly PaginationConfiguration _config;

    public OptimizedPaginationService(
        ILogger<OptimizedPaginationService>? logger = null,
        IMemoryCache? cache = null,
        PaginationConfiguration? config = null
    )
    {
        _logger = logger;
        _cache = cache;
        _config = config ?? new PaginationConfiguration();
    }

    /// <summary>
    /// Pagina una consulta con lógica optimizada y manejo robusto de errores
    /// </summary>
    public async Task<PaginatedResponseV2<T>> GetAllPaginatedAsync<T>(
        IQueryable<T> query,
        int page,
        int pageSize,
        string? orderBy = null,
        List<PaginationFilter>? filters = null,
        List<string>? includes = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var stopwatch = Stopwatch.StartNew();
        var operationId = Guid.NewGuid().ToString("N")[..8];

        try
        {
            _logger?.LogInformation(
                "Iniciando paginación optimizada [{OperationId}]: página {Page}, pageSize {PageSize}",
                operationId,
                page,
                pageSize
            );

            // Validar parámetros de entrada
            var validatedParams = ValidateAndNormalizeParameters(page, pageSize);
            page = validatedParams.Page;
            pageSize = validatedParams.PageSize;

            // Construir consulta optimizada
            var optimizedQuery = await BuildOptimizedQuery(
                query,
                filters,
                includes,
                orderBy,
                cancellationToken
            );

            // Ejecutar paginación con estrategia optimizada
            var result = await ExecuteOptimizedPagination(
                optimizedQuery,
                page,
                pageSize,
                operationId,
                cancellationToken
            );

            stopwatch.Stop();
            result.Meta.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            _logger?.LogInformation(
                "Paginación optimizada completada [{OperationId}]: {Total} elementos, tiempo: {ExecutionTime}ms",
                operationId,
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
                "Error en paginación optimizada [{OperationId}]: página {Page}, pageSize {PageSize}, tiempo: {ExecutionTime}ms",
                operationId,
                page,
                pageSize,
                stopwatch.ElapsedMilliseconds
            );

            throw new PaginationException(
                $"Error durante la paginación: {ex.Message}",
                ex,
                page,
                pageSize,
                operationId
            );
        }
    }

    /// <summary>
    /// Construye consulta optimizada con filtros, includes y ordenamiento
    /// </summary>
    private async Task<IQueryable<T>> BuildOptimizedQuery<T>(
        IQueryable<T> query,
        List<PaginationFilter>? filters,
        List<string>? includes,
        string? orderBy,
        CancellationToken cancellationToken
    )
        where T : class
    {
        _logger?.LogDebug("Construyendo consulta optimizada para {EntityType}", typeof(T).Name);

        // Aplicar includes optimizados
        if (includes?.Count > 0)
        {
            query = ApplyOptimizedIncludes(query, includes);
        }

        // Aplicar filtros optimizados
        if (filters?.Count > 0)
        {
            query = await ApplyOptimizedFilters(query, filters, cancellationToken);
        }

        // Aplicar ordenamiento optimizado
        if (!string.IsNullOrEmpty(orderBy))
        {
            query = ApplyOptimizedOrdering(query, orderBy);
        }

        return query;
    }

    /// <summary>
    /// Aplica includes optimizados basados en el tipo de entidad
    /// </summary>
    private IQueryable<T> ApplyOptimizedIncludes<T>(IQueryable<T> query, List<string> includes)
        where T : class
    {
        foreach (var include in includes)
        {
            try
            {
                query = query.Include(include);
                _logger?.LogDebug("Include aplicado: {Include}", include);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "No se pudo aplicar include: {Include}", include);
                // Continuar con otros includes
            }
        }

        return query;
    }

    /// <summary>
    /// Aplica filtros optimizados con validación y manejo de errores
    /// </summary>
    private async Task<IQueryable<T>> ApplyOptimizedFilters<T>(
        IQueryable<T> query,
        List<PaginationFilter> filters,
        CancellationToken cancellationToken
    )
    {
        foreach (var filter in filters)
        {
            try
            {
                query = ApplyFilter(query, filter);
                _logger?.LogDebug(
                    "Filtro aplicado: {Field} {Operator} {Value}",
                    filter.Field,
                    filter.Operator,
                    filter.Value
                );
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(
                    ex,
                    "No se pudo aplicar filtro: {Field} {Operator} {Value}",
                    filter.Field,
                    filter.Operator,
                    filter.Value
                );
                // Continuar con otros filtros
            }
        }

        return query;
    }

    /// <summary>
    /// Aplica un filtro individual a la consulta
    /// </summary>
    private IQueryable<T> ApplyFilter<T>(IQueryable<T> query, PaginationFilter filter)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, filter.Field);
        var constant = Expression.Constant(filter.Value);

        Expression? condition = filter.Operator.ToLower() switch
        {
            "eq" => Expression.Equal(property, constant),
            "ne" => Expression.NotEqual(property, constant),
            "gt" => Expression.GreaterThan(property, constant),
            "gte" => Expression.GreaterThanOrEqual(property, constant),
            "lt" => Expression.LessThan(property, constant),
            "lte" => Expression.LessThanOrEqual(property, constant),
            "contains" => Expression.Call(property, "Contains", null, constant),
            "startswith" => Expression.Call(property, "StartsWith", null, constant),
            "endswith" => Expression.Call(property, "EndsWith", null, constant),
            _ => throw new ArgumentException($"Operador no soportado: {filter.Operator}"),
        };

        var lambda = Expression.Lambda<Func<T, bool>>(condition, parameter);
        return query.Where(lambda);
    }

    /// <summary>
    /// Aplica ordenamiento optimizado
    /// </summary>
    private IQueryable<T> ApplyOptimizedOrdering<T>(IQueryable<T> query, string orderBy)
    {
        try
        {
            var orderParts = orderBy.Split(' ');
            var propertyName = orderParts[0];
            var direction = orderParts.Length > 1 ? orderParts[1] : "asc";

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
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "No se pudo aplicar ordenamiento: {OrderBy}", orderBy);
            // Fallback a ordenamiento por ID si existe
            if (typeof(T).GetProperty("Id") != null)
            {
                return query.OrderBy(x => EF.Property<Guid>(x, "Id"));
            }
            return query;
        }
    }

    /// <summary>
    /// Ejecuta paginación con estrategia optimizada
    /// </summary>
    private async Task<PaginatedResponseV2<T>> ExecuteOptimizedPagination<T>(
        IQueryable<T> query,
        int page,
        int pageSize,
        string operationId,
        CancellationToken cancellationToken
    )
    {
        // Determinar estrategia de paginación
        var offset = (page - 1) * pageSize;
        var useAdvancedStrategy = offset > _config.LargeOffsetThreshold;

        if (useAdvancedStrategy)
        {
            return await ExecuteAdvancedPagination(
                query,
                page,
                pageSize,
                operationId,
                cancellationToken
            );
        }

        return await ExecuteStandardPagination(
            query,
            page,
            pageSize,
            operationId,
            cancellationToken
        );
    }

    /// <summary>
    /// Ejecuta paginación estándar optimizada
    /// </summary>
    private async Task<PaginatedResponseV2<T>> ExecuteStandardPagination<T>(
        IQueryable<T> query,
        int page,
        int pageSize,
        string operationId,
        CancellationToken cancellationToken
    )
    {
        // Ejecutar secuencialmente para evitar problemas de concurrencia con DbContext
        var total = await GetOptimizedCountAsync(query, operationId, cancellationToken);
        var data = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return PaginatedResponseV2<T>.Create(data, total, page, pageSize);
    }

    /// <summary>
    /// Ejecuta paginación avanzada para offsets grandes
    /// </summary>
    private async Task<PaginatedResponseV2<T>> ExecuteAdvancedPagination<T>(
        IQueryable<T> query,
        int page,
        int pageSize,
        string operationId,
        CancellationToken cancellationToken
    )
    {
        // Para offsets grandes, usar estrategia de ID-based pagination
        if (typeof(T).GetProperty("Id") != null)
        {
            return await ExecuteIdBasedPagination(
                query,
                page,
                pageSize,
                operationId,
                cancellationToken
            );
        }

        // Fallback a paginación estándar
        return await ExecuteStandardPagination(
            query,
            page,
            pageSize,
            operationId,
            cancellationToken
        );
    }

    /// <summary>
    /// Ejecuta paginación basada en ID para offsets grandes
    /// </summary>
    private async Task<PaginatedResponseV2<T>> ExecuteIdBasedPagination<T>(
        IQueryable<T> query,
        int page,
        int pageSize,
        string operationId,
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

        // Obtener conteo total
        var total = await GetOptimizedCountAsync(query, operationId, cancellationToken);

        return PaginatedResponseV2<T>.Create(data, total, page, pageSize);
    }

    /// <summary>
    /// Obtiene conteo optimizado con caché
    /// </summary>
    private async Task<int> GetOptimizedCountAsync<T>(
        IQueryable<T> query,
        string operationId,
        CancellationToken cancellationToken
    )
    {
        var cacheKey = GenerateCountCacheKey(query, operationId);

        if (_cache != null && _config.EnableCountCache)
        {
            if (_cache.TryGetValue(cacheKey, out int cachedCount))
            {
                _logger?.LogDebug(
                    "Conteo obtenido del caché [{OperationId}]: {Count}",
                    operationId,
                    cachedCount
                );
                return cachedCount;
            }
        }

        var count = await query.CountAsync(cancellationToken);

        if (_cache != null && _config.EnableCountCache)
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _config.CountCacheExpiration,
                SlidingExpiration = TimeSpan.FromMinutes(2),
                Priority = CacheItemPriority.Normal,
                Size = 1, // Especificar tamaño para el cache
            };
            _cache.Set(cacheKey, count, cacheOptions);
        }

        _logger?.LogDebug("Conteo calculado [{OperationId}]: {Count}", operationId, count);
        return count;
    }

    /// <summary>
    /// Valida y normaliza parámetros de entrada
    /// </summary>
    private PaginationParams ValidateAndNormalizeParameters(int page, int pageSize)
    {
        // Validaciones robustas
        if (page < 1)
        {
            _logger?.LogWarning("Página inválida: {Page}, usando página 1", page);
            page = 1;
        }

        if (pageSize < 1)
        {
            _logger?.LogWarning("PageSize inválido: {PageSize}, usando pageSize 10", pageSize);
            pageSize = 10;
        }

        if (pageSize > _config.MaxPageSize)
        {
            _logger?.LogWarning(
                "PageSize excede máximo: {PageSize}, usando máximo {MaxPageSize}",
                pageSize,
                _config.MaxPageSize
            );
            pageSize = _config.MaxPageSize;
        }

        return PaginationParams.Create(page, pageSize);
    }

    /// <summary>
    /// Genera clave de caché para conteo
    /// </summary>
    private string GenerateCountCacheKey<T>(IQueryable<T> query, string operationId)
    {
        var queryString = query.ToString();
        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(queryString)
            )
        );
        return $"pagination:count:{typeof(T).Name}:{hash[..16]}:{operationId}";
    }

    /// <summary>
    /// Obtiene estadísticas de performance
    /// </summary>
    public async Task<PaginationPerformanceStats> GetPerformanceStatsAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();

        var totalCount = await query.CountAsync(cancellationToken);
        var queryString = query.ToString();

        stopwatch.Stop();

        return new PaginationPerformanceStats
        {
            TotalRecords = totalCount,
            QueryComplexity = CalculateQueryComplexity(queryString),
            EstimatedPageCount = (int)Math.Ceiling((double)totalCount / 10),
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
/// Configuración del servicio de paginación
/// </summary>
public class PaginationConfiguration
{
    public int MaxPageSize { get; set; } = 1000;
    public int DefaultPageSize { get; set; } = 10;
    public int LargeOffsetThreshold { get; set; } = 10000;
    public bool EnableCountCache { get; set; } = true;
    public TimeSpan CountCacheExpiration { get; set; } = TimeSpan.FromMinutes(10);
    public bool EnablePerformanceLogging { get; set; } = true;
    public bool EnableAdvancedPagination { get; set; } = true;
}

/// <summary>
/// Excepción personalizada para errores de paginación
/// </summary>
public class PaginationException : Exception
{
    public int Page { get; }
    public int PageSize { get; }
    public string OperationId { get; }

    public PaginationException(
        string message,
        Exception innerException,
        int page,
        int pageSize,
        string operationId
    )
        : base(message, innerException)
    {
        Page = page;
        PageSize = pageSize;
        OperationId = operationId;
    }
}
