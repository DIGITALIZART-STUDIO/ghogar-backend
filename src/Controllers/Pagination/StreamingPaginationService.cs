using System.Diagnostics;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Services;

/// <summary>
/// Servicio de paginación con streaming para grandes volúmenes de datos
/// </summary>
public class StreamingPaginationService
{
    private readonly ILogger<StreamingPaginationService>? _logger;
    private readonly PaginationConfiguration _config;

    public StreamingPaginationService(
        ILogger<StreamingPaginationService>? logger = null,
        PaginationConfiguration? config = null
    )
    {
        _logger = logger;
        _config = config ?? new PaginationConfiguration();
    }

    /// <summary>
    /// Pagina una consulta usando streaming para optimizar memoria
    /// </summary>
    public async IAsyncEnumerable<T> StreamPaginatedDataAsync<T>(
        IQueryable<T> query,
        int pageSize = 1000,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var operationId = Guid.NewGuid().ToString("N")[..8];

        _logger?.LogInformation(
            "Iniciando streaming paginado [{OperationId}]: pageSize {PageSize}",
            operationId,
            pageSize
        );

        var offset = 0;
        var hasMoreData = true;

        while (hasMoreData && !cancellationToken.IsCancellationRequested)
        {
            List<T> batch;
            try
            {
                batch = await query.Skip(offset).Take(pageSize).ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger?.LogError(
                    ex,
                    "Error en streaming paginado [{OperationId}]: tiempo: {ExecutionTime}ms",
                    operationId,
                    stopwatch.ElapsedMilliseconds
                );
                throw;
            }

            if (batch.Count == 0)
            {
                hasMoreData = false;
                break;
            }

            foreach (var item in batch)
            {
                yield return item;
            }

            offset += pageSize;

            _logger?.LogDebug(
                "Batch procesado [{OperationId}]: {Count} elementos, offset {Offset}",
                operationId,
                batch.Count,
                offset
            );
        }

        stopwatch.Stop();
        _logger?.LogInformation(
            "Streaming paginado completado [{OperationId}]: {TotalElements} elementos, tiempo: {ExecutionTime}ms",
            operationId,
            offset,
            stopwatch.ElapsedMilliseconds
        );
    }

    /// <summary>
    /// Pagina una consulta con streaming y proyección para optimizar transferencia de datos
    /// </summary>
    public async IAsyncEnumerable<TProjection> StreamPaginatedProjectionAsync<T, TProjection>(
        IQueryable<T> query,
        System.Linq.Expressions.Expression<Func<T, TProjection>> projection,
        int pageSize = 1000,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var operationId = Guid.NewGuid().ToString("N")[..8];

        _logger?.LogInformation(
            "Iniciando streaming con proyección [{OperationId}]: pageSize {PageSize}",
            operationId,
            pageSize
        );

        var offset = 0;
        var hasMoreData = true;

        while (hasMoreData && !cancellationToken.IsCancellationRequested)
        {
            List<TProjection> batch;
            try
            {
                batch = await query
                    .Skip(offset)
                    .Take(pageSize)
                    .Select(projection)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger?.LogError(
                    ex,
                    "Error en streaming con proyección [{OperationId}]: tiempo: {ExecutionTime}ms",
                    operationId,
                    stopwatch.ElapsedMilliseconds
                );
                throw;
            }

            if (batch.Count == 0)
            {
                hasMoreData = false;
                break;
            }

            foreach (var item in batch)
            {
                yield return item;
            }

            offset += pageSize;

            _logger?.LogDebug(
                "Batch con proyección procesado [{OperationId}]: {Count} elementos, offset {Offset}",
                operationId,
                batch.Count,
                offset
            );
        }

        stopwatch.Stop();
        _logger?.LogInformation(
            "Streaming con proyección completado [{OperationId}]: {TotalElements} elementos, tiempo: {ExecutionTime}ms",
            operationId,
            offset,
            stopwatch.ElapsedMilliseconds
        );
    }

    /// <summary>
    /// Pagina una consulta con streaming y filtros dinámicos
    /// </summary>
    public async IAsyncEnumerable<T> StreamPaginatedWithFiltersAsync<T>(
        IQueryable<T> query,
        List<PaginationFilter> filters,
        int pageSize = 1000,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var operationId = Guid.NewGuid().ToString("N")[..8];

        _logger?.LogInformation(
            "Iniciando streaming con filtros [{OperationId}]: pageSize {PageSize}, filtros: {FilterCount}",
            operationId,
            pageSize,
            filters.Count
        );

        // Aplicar filtros
        var filteredQuery = ApplyFiltersToQuery(query, filters);

        var offset = 0;
        var hasMoreData = true;

        while (hasMoreData && !cancellationToken.IsCancellationRequested)
        {
            List<T> batch;
            try
            {
                batch = await filteredQuery
                    .Skip(offset)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger?.LogError(
                    ex,
                    "Error en streaming con filtros [{OperationId}]: tiempo: {ExecutionTime}ms",
                    operationId,
                    stopwatch.ElapsedMilliseconds
                );
                throw;
            }

            if (batch.Count == 0)
            {
                hasMoreData = false;
                break;
            }

            foreach (var item in batch)
            {
                yield return item;
            }

            offset += pageSize;

            _logger?.LogDebug(
                "Batch con filtros procesado [{OperationId}]: {Count} elementos, offset {Offset}",
                operationId,
                batch.Count,
                offset
            );
        }

        stopwatch.Stop();
        _logger?.LogInformation(
            "Streaming con filtros completado [{OperationId}]: {TotalElements} elementos, tiempo: {ExecutionTime}ms",
            operationId,
            offset,
            stopwatch.ElapsedMilliseconds
        );
    }

    /// <summary>
    /// Aplica filtros a la consulta
    /// </summary>
    private IQueryable<T> ApplyFiltersToQuery<T>(
        IQueryable<T> query,
        List<PaginationFilter> filters
    )
    {
        foreach (var filter in filters)
        {
            try
            {
                query = ApplyFilter(query, filter);
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
    /// Aplica un filtro individual a la consulta
    /// </summary>
    private IQueryable<T> ApplyFilter<T>(IQueryable<T> query, PaginationFilter filter)
    {
        var parameter = System.Linq.Expressions.Expression.Parameter(typeof(T), "x");
        var property = System.Linq.Expressions.Expression.Property(parameter, filter.Field);
        var constant = System.Linq.Expressions.Expression.Constant(filter.Value);

        System.Linq.Expressions.Expression? condition = filter.Operator.ToLower() switch
        {
            "eq" => System.Linq.Expressions.Expression.Equal(property, constant),
            "ne" => System.Linq.Expressions.Expression.NotEqual(property, constant),
            "gt" => System.Linq.Expressions.Expression.GreaterThan(property, constant),
            "gte" => System.Linq.Expressions.Expression.GreaterThanOrEqual(property, constant),
            "lt" => System.Linq.Expressions.Expression.LessThan(property, constant),
            "lte" => System.Linq.Expressions.Expression.LessThanOrEqual(property, constant),
            "contains" => System.Linq.Expressions.Expression.Call(
                property,
                "Contains",
                null,
                constant
            ),
            "startswith" => System.Linq.Expressions.Expression.Call(
                property,
                "StartsWith",
                null,
                constant
            ),
            "endswith" => System.Linq.Expressions.Expression.Call(
                property,
                "EndsWith",
                null,
                constant
            ),
            _ => throw new ArgumentException($"Operador no soportado: {filter.Operator}"),
        };

        var lambda = System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(condition, parameter);
        return query.Where(lambda);
    }

    /// <summary>
    /// Obtiene estadísticas de streaming
    /// </summary>
    public async Task<StreamingStats> GetStreamingStatsAsync<T>(
        IQueryable<T> query,
        int pageSize = 1000,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();

        var totalCount = await query.CountAsync(cancellationToken);
        var estimatedBatches = (int)Math.Ceiling((double)totalCount / pageSize);

        stopwatch.Stop();

        return new StreamingStats
        {
            TotalRecords = totalCount,
            PageSize = pageSize,
            EstimatedBatches = estimatedBatches,
            EstimatedMemoryUsageMB = EstimateMemoryUsage(totalCount, typeof(T)),
            CountExecutionTimeMs = stopwatch.ElapsedMilliseconds,
        };
    }

    /// <summary>
    /// Estima el uso de memoria
    /// </summary>
    private double EstimateMemoryUsage(int recordCount, Type recordType)
    {
        // Estimación básica del tamaño de un objeto
        var estimatedObjectSize = recordType.GetProperties().Length * 50; // 50 bytes por propiedad
        var totalSizeBytes = recordCount * estimatedObjectSize;
        return totalSizeBytes / (1024.0 * 1024.0); // Convertir a MB
    }
}

/// <summary>
/// Estadísticas de streaming
/// </summary>
public class StreamingStats
{
    public int TotalRecords { get; set; }
    public int PageSize { get; set; }
    public int EstimatedBatches { get; set; }
    public double EstimatedMemoryUsageMB { get; set; }
    public long CountExecutionTimeMs { get; set; }
}
