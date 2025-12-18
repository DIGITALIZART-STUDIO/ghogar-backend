using System.Diagnostics;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GestionHogar.Services;

/// <summary>
/// Servicio de métricas y monitoreo para paginación
/// </summary>
public class PaginationMetricsService
{
    private readonly ILogger<PaginationMetricsService>? _logger;
    private readonly IMemoryCache? _cache;
    private readonly List<PaginationMetric> _metrics;

    public PaginationMetricsService(
        ILogger<PaginationMetricsService>? logger = null,
        IMemoryCache? cache = null
    )
    {
        _logger = logger;
        _cache = cache;
        _metrics = new List<PaginationMetric>();
    }

    /// <summary>
    /// Registra una métrica de paginación
    /// </summary>
    public void RecordMetric(PaginationMetric metric)
    {
        _metrics.Add(metric);
        _logger?.LogDebug(
            "Métrica registrada: {OperationType} - {ExecutionTime}ms - {TotalRecords} registros",
            metric.OperationType,
            metric.ExecutionTimeMs,
            metric.TotalRecords
        );

        // Limpiar métricas antiguas (mantener solo las últimas 1000)
        if (_metrics.Count > 1000)
        {
            _metrics.RemoveRange(0, _metrics.Count - 1000);
        }
    }

    /// <summary>
    /// Obtiene estadísticas de performance
    /// </summary>
    public PaginationPerformanceReport GetPerformanceReport(TimeSpan? timeWindow = null)
    {
        var cutoffTime = timeWindow.HasValue
            ? DateTime.UtcNow - timeWindow.Value
            : DateTime.MinValue;

        var relevantMetrics = _metrics.Where(m => m.Timestamp >= cutoffTime).ToList();

        if (!relevantMetrics.Any())
        {
            return new PaginationPerformanceReport();
        }

        var avgExecutionTime = relevantMetrics.Average(m => m.ExecutionTimeMs);
        var maxExecutionTime = relevantMetrics.Max(m => m.ExecutionTimeMs);
        var minExecutionTime = relevantMetrics.Min(m => m.ExecutionTimeMs);
        var totalOperations = relevantMetrics.Count;
        var totalRecords = relevantMetrics.Sum(m => m.TotalRecords);

        var operationTypes = relevantMetrics
            .GroupBy(m => m.OperationType)
            .ToDictionary(g => g.Key, g => g.Count());

        var slowOperations = relevantMetrics
            .Where(m => m.ExecutionTimeMs > avgExecutionTime * 2)
            .ToList();

        return new PaginationPerformanceReport
        {
            TimeWindow = timeWindow ?? TimeSpan.FromHours(24),
            TotalOperations = totalOperations,
            TotalRecordsProcessed = totalRecords,
            AverageExecutionTimeMs = avgExecutionTime,
            MaxExecutionTimeMs = maxExecutionTime,
            MinExecutionTimeMs = minExecutionTime,
            OperationTypeCounts = operationTypes,
            SlowOperationsCount = slowOperations.Count,
            SlowOperations = slowOperations.Take(10).ToList(),
            PerformanceScore = CalculatePerformanceScore(
                avgExecutionTime,
                slowOperations.Count,
                totalOperations
            ),
        };
    }

    /// <summary>
    /// Obtiene métricas por tipo de operación
    /// </summary>
    public Dictionary<string, OperationMetrics> GetMetricsByOperationType(
        TimeSpan? timeWindow = null
    )
    {
        var cutoffTime = timeWindow.HasValue
            ? DateTime.UtcNow - timeWindow.Value
            : DateTime.MinValue;

        return _metrics
            .Where(m => m.Timestamp >= cutoffTime)
            .GroupBy(m => m.OperationType)
            .ToDictionary(
                g => g.Key,
                g => new OperationMetrics
                {
                    OperationType = g.Key,
                    Count = g.Count(),
                    AverageExecutionTimeMs = g.Average(m => m.ExecutionTimeMs),
                    MaxExecutionTimeMs = g.Max(m => m.ExecutionTimeMs),
                    MinExecutionTimeMs = g.Min(m => m.ExecutionTimeMs),
                    TotalRecords = g.Sum(m => m.TotalRecords),
                    AveragePageSize = g.Average(m => m.PageSize),
                    AveragePage = g.Average(m => m.Page),
                }
            );
    }

    /// <summary>
    /// Obtiene métricas de performance por entidad
    /// </summary>
    public Dictionary<string, EntityMetrics> GetMetricsByEntity(TimeSpan? timeWindow = null)
    {
        var cutoffTime = timeWindow.HasValue
            ? DateTime.UtcNow - timeWindow.Value
            : DateTime.MinValue;

        return _metrics
            .Where(m => m.Timestamp >= cutoffTime)
            .GroupBy(m => m.EntityType)
            .ToDictionary(
                g => g.Key,
                g => new EntityMetrics
                {
                    EntityType = g.Key,
                    Count = g.Count(),
                    AverageExecutionTimeMs = g.Average(m => m.ExecutionTimeMs),
                    MaxExecutionTimeMs = g.Max(m => m.ExecutionTimeMs),
                    TotalRecords = g.Sum(m => m.TotalRecords),
                    AveragePageSize = g.Average(m => m.PageSize),
                    MostCommonPageSize = g.GroupBy(m => m.PageSize)
                        .OrderByDescending(x => x.Count())
                        .First()
                        .Key,
                }
            );
    }

    /// <summary>
    /// Detecta operaciones lentas
    /// </summary>
    public List<PaginationMetric> DetectSlowOperations(
        double thresholdMultiplier = 2.0,
        TimeSpan? timeWindow = null
    )
    {
        var cutoffTime = timeWindow.HasValue
            ? DateTime.UtcNow - timeWindow.Value
            : DateTime.MinValue;

        var relevantMetrics = _metrics.Where(m => m.Timestamp >= cutoffTime).ToList();

        if (!relevantMetrics.Any())
        {
            return new List<PaginationMetric>();
        }

        var avgExecutionTime = relevantMetrics.Average(m => m.ExecutionTimeMs);
        var threshold = avgExecutionTime * thresholdMultiplier;

        return relevantMetrics
            .Where(m => m.ExecutionTimeMs > threshold)
            .OrderByDescending(m => m.ExecutionTimeMs)
            .ToList();
    }

    /// <summary>
    /// Obtiene recomendaciones de optimización
    /// </summary>
    public List<OptimizationRecommendation> GetOptimizationRecommendations()
    {
        var recommendations = new List<OptimizationRecommendation>();
        var report = GetPerformanceReport();

        // Recomendación basada en tiempo de ejecución promedio
        if (report.AverageExecutionTimeMs > 1000)
        {
            recommendations.Add(
                new OptimizationRecommendation
                {
                    Type = "Performance",
                    Priority = "High",
                    Title = "Tiempo de ejecución alto",
                    Description =
                        $"El tiempo promedio de ejecución es {report.AverageExecutionTimeMs:F2}ms, considere optimizar consultas o implementar caché.",
                    SuggestedActions = new[]
                    {
                        "Implementar caché para consultas frecuentes",
                        "Optimizar índices de base de datos",
                        "Revisar consultas con JOIN complejos",
                        "Considerar paginación basada en cursor para offsets grandes",
                    },
                }
            );
        }

        // Recomendación basada en operaciones lentas
        if (report.SlowOperationsCount > report.TotalOperations * 0.1)
        {
            recommendations.Add(
                new OptimizationRecommendation
                {
                    Type = "Performance",
                    Priority = "Medium",
                    Title = "Alto número de operaciones lentas",
                    Description =
                        $"{report.SlowOperationsCount} de {report.TotalOperations} operaciones son lentas.",
                    SuggestedActions = new[]
                    {
                        "Analizar patrones en operaciones lentas",
                        "Implementar monitoreo en tiempo real",
                        "Considerar particionamiento de datos",
                    },
                }
            );
        }

        // Recomendación basada en tamaño de página
        var largePageSizeOperations = _metrics.Count(m => m.PageSize > 100);
        if (largePageSizeOperations > _metrics.Count * 0.2)
        {
            recommendations.Add(
                new OptimizationRecommendation
                {
                    Type = "Configuration",
                    Priority = "Low",
                    Title = "Uso frecuente de páginas grandes",
                    Description = $"{largePageSizeOperations} operaciones usan pageSize > 100.",
                    SuggestedActions = new[]
                    {
                        "Considerar límites más estrictos para pageSize",
                        "Implementar paginación basada en cursor para páginas grandes",
                        "Educar a los desarrolladores sobre mejores prácticas",
                    },
                }
            );
        }

        return recommendations;
    }

    /// <summary>
    /// Calcula score de performance
    /// </summary>
    private double CalculatePerformanceScore(
        double avgExecutionTime,
        int slowOperationsCount,
        int totalOperations
    )
    {
        // Score basado en tiempo de ejecución (0-50 puntos)
        var timeScore = Math.Max(0, 50 - (avgExecutionTime / 100));

        // Score basado en operaciones lentas (0-30 puntos)
        var slowOperationsRatio =
            totalOperations > 0 ? (double)slowOperationsCount / totalOperations : 0;
        var slowOperationsScore = Math.Max(0, 30 - (slowOperationsRatio * 100));

        // Score basado en volumen (0-20 puntos)
        var volumeScore = Math.Min(20, totalOperations / 10);

        return Math.Min(100, timeScore + slowOperationsScore + volumeScore);
    }

    /// <summary>
    /// Limpia métricas antiguas
    /// </summary>
    public void CleanupOldMetrics(TimeSpan retentionPeriod)
    {
        var cutoffTime = DateTime.UtcNow - retentionPeriod;
        var removedCount = _metrics.RemoveAll(m => m.Timestamp < cutoffTime);

        _logger?.LogInformation(
            "Limpiadas {Count} métricas antiguas (más de {RetentionPeriod})",
            removedCount,
            retentionPeriod
        );
    }

    /// <summary>
    /// Exporta métricas a formato JSON
    /// </summary>
    public string ExportMetricsToJson(TimeSpan? timeWindow = null)
    {
        var cutoffTime = timeWindow.HasValue
            ? DateTime.UtcNow - timeWindow.Value
            : DateTime.MinValue;

        var relevantMetrics = _metrics
            .Where(m => m.Timestamp >= cutoffTime)
            .Select(m => new
            {
                m.Timestamp,
                m.OperationType,
                m.EntityType,
                m.Page,
                m.PageSize,
                m.TotalRecords,
                m.ExecutionTimeMs,
                m.OperationId,
            })
            .ToList();

        return System.Text.Json.JsonSerializer.Serialize(
            relevantMetrics,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
        );
    }
}

/// <summary>
/// Métrica de paginación
/// </summary>
public class PaginationMetric
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string OperationType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalRecords { get; set; }
    public long ExecutionTimeMs { get; set; }
    public string OperationId { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public bool IsSuccess { get; set; } = true;
}

/// <summary>
/// Reporte de performance de paginación
/// </summary>
public class PaginationPerformanceReport
{
    public TimeSpan TimeWindow { get; set; }
    public int TotalOperations { get; set; }
    public int TotalRecordsProcessed { get; set; }
    public double AverageExecutionTimeMs { get; set; }
    public long MaxExecutionTimeMs { get; set; }
    public long MinExecutionTimeMs { get; set; }
    public Dictionary<string, int> OperationTypeCounts { get; set; } = new();
    public int SlowOperationsCount { get; set; }
    public List<PaginationMetric> SlowOperations { get; set; } = new();
    public double PerformanceScore { get; set; }
}

/// <summary>
/// Métricas por tipo de operación
/// </summary>
public class OperationMetrics
{
    public string OperationType { get; set; } = string.Empty;
    public int Count { get; set; }
    public double AverageExecutionTimeMs { get; set; }
    public long MaxExecutionTimeMs { get; set; }
    public long MinExecutionTimeMs { get; set; }
    public int TotalRecords { get; set; }
    public double AveragePageSize { get; set; }
    public double AveragePage { get; set; }
}

/// <summary>
/// Métricas por entidad
/// </summary>
public class EntityMetrics
{
    public string EntityType { get; set; } = string.Empty;
    public int Count { get; set; }
    public double AverageExecutionTimeMs { get; set; }
    public long MaxExecutionTimeMs { get; set; }
    public int TotalRecords { get; set; }
    public double AveragePageSize { get; set; }
    public int MostCommonPageSize { get; set; }
}

/// <summary>
/// Recomendación de optimización
/// </summary>
public class OptimizationRecommendation
{
    public string Type { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[] SuggestedActions { get; set; } = Array.Empty<string>();
}
