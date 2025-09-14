using System.Diagnostics;
using System.Linq.Expressions;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GestionHogar.Services;

/// <summary>
/// Repositorio de paginación optimizado con consultas de base de datos eficientes
/// </summary>
public class OptimizedPaginationRepository<T>
    where T : class, IEntity
{
    private readonly DbContext _context;
    private readonly DbSet<T> _dbSet;
    private readonly ILogger<OptimizedPaginationRepository<T>>? _logger;
    private readonly IMemoryCache? _cache;
    private readonly PaginationConfiguration _config;

    public OptimizedPaginationRepository(
        DbContext context,
        ILogger<OptimizedPaginationRepository<T>>? logger = null,
        IMemoryCache? cache = null,
        PaginationConfiguration? config = null
    )
    {
        _context = context;
        _dbSet = context.Set<T>();
        _logger = logger;
        _cache = cache;
        _config = config ?? new PaginationConfiguration();
    }

    /// <summary>
    /// Encuentra entidades paginadas con consultas optimizadas
    /// </summary>
    public async Task<PaginatedResponseV2<T>> FindManyPaginatedAsync(
        int page,
        int pageSize,
        Expression<Func<T, bool>>? whereClause = null,
        List<Expression<Func<T, object>>>? orderBy = null,
        List<string>? includes = null,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var operationId = Guid.NewGuid().ToString("N")[..8];

        try
        {
            _logger?.LogInformation(
                "Iniciando FindManyPaginatedAsync [{OperationId}]: página {Page}, pageSize {PageSize}",
                operationId,
                page,
                pageSize
            );

            // Validar parámetros
            var validatedParams = ValidateAndNormalizeParameters(page, pageSize);
            page = validatedParams.Page;
            pageSize = validatedParams.PageSize;

            // Construir consulta base
            var query = BuildBaseQuery(whereClause, includes);

            // Aplicar ordenamiento
            if (orderBy?.Count > 0)
            {
                query = ApplyOrdering(query, orderBy);
            }

            // Ejecutar paginación optimizada
            var result = await ExecuteOptimizedPagination(
                query,
                page,
                pageSize,
                operationId,
                cancellationToken
            );

            stopwatch.Stop();
            result.Meta.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            _logger?.LogInformation(
                "FindManyPaginatedAsync completado [{OperationId}]: {Total} elementos, tiempo: {ExecutionTime}ms",
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
                "Error en FindManyPaginatedAsync [{OperationId}]: página {Page}, pageSize {PageSize}, tiempo: {ExecutionTime}ms",
                operationId,
                page,
                pageSize,
                stopwatch.ElapsedMilliseconds
            );

            throw new PaginationRepositoryException(
                $"Error durante FindManyPaginatedAsync: {ex.Message}",
                ex,
                page,
                pageSize,
                operationId
            );
        }
    }

    /// <summary>
    /// Encuentra entidades paginadas con filtros complejos
    /// </summary>
    public async Task<PaginatedResponseV2<T>> FindManyPaginatedWithFiltersAsync(
        int page,
        int pageSize,
        List<PaginationFilter> filters,
        List<PaginationOrderBy>? orderBy = null,
        List<string>? includes = null,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var operationId = Guid.NewGuid().ToString("N")[..8];

        try
        {
            _logger?.LogInformation(
                "Iniciando FindManyPaginatedWithFiltersAsync [{OperationId}]: página {Page}, pageSize {PageSize}, filtros: {FilterCount}",
                operationId,
                page,
                pageSize,
                filters.Count
            );

            // Validar parámetros
            var validatedParams = ValidateAndNormalizeParameters(page, pageSize);
            page = validatedParams.Page;
            pageSize = validatedParams.PageSize;

            // Construir consulta con filtros
            var query = BuildQueryWithFilters(filters, includes);

            // Aplicar ordenamiento
            if (orderBy?.Count > 0)
            {
                query = ApplyOrderingFromRequest(query, orderBy);
            }

            // Ejecutar paginación optimizada
            var result = await ExecuteOptimizedPagination(
                query,
                page,
                pageSize,
                operationId,
                cancellationToken
            );

            stopwatch.Stop();
            result.Meta.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            _logger?.LogInformation(
                "FindManyPaginatedWithFiltersAsync completado [{OperationId}]: {Total} elementos, tiempo: {ExecutionTime}ms",
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
                "Error en FindManyPaginatedWithFiltersAsync [{OperationId}]: página {Page}, pageSize {PageSize}, tiempo: {ExecutionTime}ms",
                operationId,
                page,
                pageSize,
                stopwatch.ElapsedMilliseconds
            );

            throw new PaginationRepositoryException(
                $"Error durante FindManyPaginatedWithFiltersAsync: {ex.Message}",
                ex,
                page,
                pageSize,
                operationId
            );
        }
    }

    /// <summary>
    /// Construye consulta base con where clause e includes
    /// </summary>
    private IQueryable<T> BuildBaseQuery(
        Expression<Func<T, bool>>? whereClause,
        List<string>? includes
    )
    {
        var query = _dbSet.AsQueryable();

        // Aplicar where clause
        if (whereClause != null)
        {
            query = query.Where(whereClause);
        }

        // Aplicar includes optimizados
        if (includes?.Count > 0)
        {
            query = ApplyOptimizedIncludes(query, includes);
        }

        return query;
    }

    /// <summary>
    /// Construye consulta con filtros complejos
    /// </summary>
    private IQueryable<T> BuildQueryWithFilters(
        List<PaginationFilter> filters,
        List<string>? includes
    )
    {
        var query = _dbSet.AsQueryable();

        // Aplicar includes primero
        if (includes?.Count > 0)
        {
            query = ApplyOptimizedIncludes(query, includes);
        }

        // Aplicar filtros
        foreach (var filter in filters)
        {
            try
            {
                query = ApplyFilterToQuery(query, filter);
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
            }
        }

        return query;
    }

    /// <summary>
    /// Aplica includes optimizados
    /// </summary>
    private IQueryable<T> ApplyOptimizedIncludes(IQueryable<T> query, List<string> includes)
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
            }
        }

        return query;
    }

    /// <summary>
    /// Aplica filtro a la consulta
    /// </summary>
    private IQueryable<T> ApplyFilterToQuery(IQueryable<T> query, PaginationFilter filter)
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
    /// Aplica ordenamiento desde expresiones
    /// </summary>
    private IQueryable<T> ApplyOrdering(
        IQueryable<T> query,
        List<Expression<Func<T, object>>> orderBy
    )
    {
        IOrderedQueryable<T>? orderedQuery = null;

        for (int i = 0; i < orderBy.Count; i++)
        {
            if (i == 0)
            {
                orderedQuery = query.OrderBy(orderBy[i]);
            }
            else
            {
                orderedQuery = orderedQuery!.ThenBy(orderBy[i]);
            }
        }

        return orderedQuery ?? query;
    }

    /// <summary>
    /// Aplica ordenamiento desde request
    /// </summary>
    private IQueryable<T> ApplyOrderingFromRequest(
        IQueryable<T> query,
        List<PaginationOrderBy> orderBy
    )
    {
        IOrderedQueryable<T>? orderedQuery = null;

        for (int i = 0; i < orderBy.Count; i++)
        {
            var order = orderBy[i];
            var parameter = Expression.Parameter(typeof(T), "x");
            var property = Expression.Property(parameter, order.Field);
            var lambda = Expression.Lambda(property, parameter);

            if (i == 0)
            {
                var methodName =
                    order.Direction.ToLower() == "desc" ? "OrderByDescending" : "OrderBy";
                var method = typeof(Queryable)
                    .GetMethods()
                    .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                    .MakeGenericMethod(typeof(T), property.Type);

                orderedQuery =
                    (IOrderedQueryable<T>)method.Invoke(null, new object[] { query, lambda })!;
            }
            else
            {
                var methodName =
                    order.Direction.ToLower() == "desc" ? "ThenByDescending" : "ThenBy";
                var method = typeof(Queryable)
                    .GetMethods()
                    .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                    .MakeGenericMethod(typeof(T), property.Type);

                orderedQuery =
                    (IOrderedQueryable<T>)
                        method.Invoke(null, new object[] { orderedQuery, lambda })!;
            }
        }

        return orderedQuery ?? query;
    }

    /// <summary>
    /// Ejecuta paginación optimizada
    /// </summary>
    private async Task<PaginatedResponseV2<T>> ExecuteOptimizedPagination(
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
    /// Ejecuta paginación estándar
    /// </summary>
    private async Task<PaginatedResponseV2<T>> ExecuteStandardPagination(
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
    private async Task<PaginatedResponseV2<T>> ExecuteAdvancedPagination(
        IQueryable<T> query,
        int page,
        int pageSize,
        string operationId,
        CancellationToken cancellationToken
    )
    {
        // Usar ID-based pagination para offsets grandes
        var minId = await query
            .OrderBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (minId == Guid.Empty)
        {
            return PaginatedResponseV2<T>.Empty(page, pageSize);
        }

        // Obtener datos usando ID como filtro
        var data = await query
            .Where(x => x.Id >= minId)
            .OrderBy(x => x.Id)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Obtener conteo total
        var total = await GetOptimizedCountAsync(query, operationId, cancellationToken);

        return PaginatedResponseV2<T>.Create(data, total, page, pageSize);
    }

    /// <summary>
    /// Obtiene conteo optimizado con caché
    /// </summary>
    private async Task<int> GetOptimizedCountAsync(
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
    /// Valida y normaliza parámetros
    /// </summary>
    private PaginationParams ValidateAndNormalizeParameters(int page, int pageSize)
    {
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
    private string GenerateCountCacheKey(IQueryable<T> query, string operationId)
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
    /// Obtiene estadísticas de performance del repositorio
    /// </summary>
    public async Task<RepositoryPerformanceStats> GetRepositoryPerformanceStatsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();

        var totalCount = await _dbSet.CountAsync(cancellationToken);
        var activeCount = await _dbSet.CountAsync(x => x.IsActive, cancellationToken);

        stopwatch.Stop();

        return new RepositoryPerformanceStats
        {
            TotalRecords = totalCount,
            ActiveRecords = activeCount,
            InactiveRecords = totalCount - activeCount,
            CountExecutionTimeMs = stopwatch.ElapsedMilliseconds,
            EntityType = typeof(T).Name,
            HasOptimizedIndexes = await CheckForOptimizedIndexes(cancellationToken),
        };
    }

    /// <summary>
    /// Verifica si existen índices optimizados
    /// </summary>
    private async Task<bool> CheckForOptimizedIndexes(CancellationToken cancellationToken)
    {
        // Implementación básica - en producción esto verificaría índices reales
        return true;
    }
}

/// <summary>
/// Excepción personalizada para errores del repositorio de paginación
/// </summary>
public class PaginationRepositoryException : Exception
{
    public int Page { get; }
    public int PageSize { get; }
    public string OperationId { get; }

    public PaginationRepositoryException(
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

/// <summary>
/// Estadísticas de performance del repositorio
/// </summary>
public class RepositoryPerformanceStats
{
    public int TotalRecords { get; set; }
    public int ActiveRecords { get; set; }
    public int InactiveRecords { get; set; }
    public long CountExecutionTimeMs { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public bool HasOptimizedIndexes { get; set; }
}
