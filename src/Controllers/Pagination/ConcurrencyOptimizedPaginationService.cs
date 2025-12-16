using System.Diagnostics;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GestionHogar.Services;

/// <summary>
/// Servicio de paginación optimizado para concurrencia y transacciones
/// </summary>
public class ConcurrencyOptimizedPaginationService
{
    private readonly ILogger<ConcurrencyOptimizedPaginationService>? _logger;
    private readonly IMemoryCache? _cache;
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly ConcurrencyPaginationConfiguration _config;

    public ConcurrencyOptimizedPaginationService(
        ILogger<ConcurrencyOptimizedPaginationService>? logger = null,
        IMemoryCache? cache = null,
        ConcurrencyPaginationConfiguration? config = null
    )
    {
        _logger = logger;
        _cache = cache;
        _config = config ?? new ConcurrencyPaginationConfiguration();
        _concurrencySemaphore = new SemaphoreSlim(
            _config.MaxConcurrentOperations,
            _config.MaxConcurrentOperations
        );
    }

    /// <summary>
    /// Pagina una consulta con optimizaciones de concurrencia
    /// </summary>
    public async Task<PaginatedResponseV2<T>> PaginateWithConcurrencyControlAsync<T>(
        IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default
    )
    {
        await _concurrencySemaphore.WaitAsync(cancellationToken);

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var operationId = Guid.NewGuid().ToString("N")[..8];

            _logger?.LogInformation(
                "Iniciando paginación con control de concurrencia [{OperationId}]: página {Page}, pageSize {PageSize}",
                operationId,
                page,
                pageSize
            );

            // Validar parámetros
            var validatedParams = ValidateAndNormalizeParameters(page, pageSize);
            page = validatedParams.Page;
            pageSize = validatedParams.PageSize;

            // Ejecutar paginación con estrategia optimizada para concurrencia
            var result = await ExecuteConcurrencyOptimizedPagination(
                query,
                page,
                pageSize,
                operationId,
                cancellationToken
            );

            stopwatch.Stop();
            result.Meta.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            _logger?.LogInformation(
                "Paginación con control de concurrencia completada [{OperationId}]: {Total} elementos, tiempo: {ExecutionTime}ms",
                operationId,
                result.Meta.Total,
                stopwatch.ElapsedMilliseconds
            );

            return result;
        }
        finally
        {
            _concurrencySemaphore.Release();
        }
    }

    /// <summary>
    /// Ejecuta paginación optimizada para concurrencia
    /// </summary>
    private async Task<PaginatedResponseV2<T>> ExecuteConcurrencyOptimizedPagination<T>(
        IQueryable<T> query,
        int page,
        int pageSize,
        string operationId,
        CancellationToken cancellationToken
    )
    {
        // Usar ReadUncommitted para consultas de conteo para mejor performance
        using var transaction = await BeginReadUncommittedTransactionAsync(cancellationToken);

        try
        {
            // Ejecutar conteo y datos en paralelo con control de concurrencia
            var totalTask = GetConcurrencyOptimizedCountAsync(
                query,
                operationId,
                cancellationToken
            );
            var dataTask = GetConcurrencyOptimizedDataAsync(
                query,
                page,
                pageSize,
                operationId,
                cancellationToken
            );

            await Task.WhenAll(totalTask, dataTask);

            var total = await totalTask;
            var data = await dataTask;

            await transaction.CommitAsync(cancellationToken);

            return PaginatedResponseV2<T>.Create(data, total, page, pageSize);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger?.LogError(
                ex,
                "Error en paginación con concurrencia [{OperationId}]",
                operationId
            );
            throw;
        }
    }

    /// <summary>
    /// Obtiene conteo optimizado para concurrencia
    /// </summary>
    private async Task<int> GetConcurrencyOptimizedCountAsync<T>(
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

        // Usar consulta optimizada para concurrencia
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
    /// Obtiene datos optimizados para concurrencia
    /// </summary>
    private async Task<List<T>> GetConcurrencyOptimizedDataAsync<T>(
        IQueryable<T> query,
        int page,
        int pageSize,
        string operationId,
        CancellationToken cancellationToken
    )
    {
        var offset = (page - 1) * pageSize;

        // Usar consulta optimizada con hints de concurrencia
        var data = await query.Skip(offset).Take(pageSize).ToListAsync(cancellationToken);

        _logger?.LogDebug(
            "Datos obtenidos [{OperationId}]: {Count} elementos",
            operationId,
            data.Count
        );
        return data;
    }

    /// <summary>
    /// Inicia transacción con isolation level ReadUncommitted
    /// </summary>
    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginReadUncommittedTransactionAsync(
        CancellationToken cancellationToken
    )
    {
        // Esta es una implementación simplificada
        // En producción, esto usaría el DbContext real
        throw new NotImplementedException("Implementación requerida del DbContext");
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
    private string GenerateCountCacheKey<T>(IQueryable<T> query, string operationId)
    {
        var queryString = query.ToString();
        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(queryString)
            )
        );
        return $"concurrency:pagination:count:{typeof(T).Name}:{hash[..16]}:{operationId}";
    }

    /// <summary>
    /// Obtiene estadísticas de concurrencia
    /// </summary>
    public ConcurrencyStats GetConcurrencyStats()
    {
        return new ConcurrencyStats
        {
            MaxConcurrentOperations = _config.MaxConcurrentOperations,
            CurrentConcurrentOperations =
                _config.MaxConcurrentOperations - _concurrencySemaphore.CurrentCount,
            AvailableSlots = _concurrencySemaphore.CurrentCount,
            LastUpdated = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Libera recursos
    /// </summary>
    public void Dispose()
    {
        _concurrencySemaphore?.Dispose();
    }
}

/// <summary>
/// Estadísticas de concurrencia
/// </summary>
public class ConcurrencyStats
{
    public int MaxConcurrentOperations { get; set; }
    public int CurrentConcurrentOperations { get; set; }
    public int AvailableSlots { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Configuración extendida para concurrencia
/// </summary>
public class ConcurrencyPaginationConfiguration : PaginationConfiguration
{
    public int MaxConcurrentOperations { get; set; } = 10;
    public bool EnableConcurrencyControl { get; set; } = true;
    public bool UseReadUncommittedForCounts { get; set; } = true;
    public TimeSpan ConcurrencyTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
