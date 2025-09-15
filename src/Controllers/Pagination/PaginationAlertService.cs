using GestionHogar.Model;
using Microsoft.Extensions.Caching.Memory;

namespace GestionHogar.Services;

/// <summary>
/// Servicio de alertas y monitoreo avanzado para paginación
/// </summary>
public class PaginationAlertService
{
    private readonly ILogger<PaginationAlertService>? _logger;
    private readonly IMemoryCache? _cache;
    private readonly List<PaginationAlert> _alerts;
    private readonly PaginationAlertConfiguration _config;

    public PaginationAlertService(
        ILogger<PaginationAlertService>? logger = null,
        IMemoryCache? cache = null,
        PaginationAlertConfiguration? config = null
    )
    {
        _logger = logger;
        _cache = cache;
        _alerts = new List<PaginationAlert>();
        _config = config ?? new PaginationAlertConfiguration();
    }

    /// <summary>
    /// Registra una métrica y evalúa alertas
    /// </summary>
    public void RecordMetricAndEvaluateAlerts(PaginationMetric metric)
    {
        try
        {
            // Evaluar alertas de performance
            EvaluatePerformanceAlerts(metric);

            // Evaluar alertas de uso
            EvaluateUsageAlerts(metric);

            // Evaluar alertas de errores
            EvaluateErrorAlerts(metric);

            // Evaluar alertas de concurrencia
            EvaluateConcurrencyAlerts(metric);
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Error evaluando alertas para métrica: {OperationId}",
                metric.OperationId
            );
        }
    }

    /// <summary>
    /// Evalúa alertas de performance
    /// </summary>
    private void EvaluatePerformanceAlerts(PaginationMetric metric)
    {
        // Alerta por tiempo de ejecución alto
        if (metric.ExecutionTimeMs > _config.HighExecutionTimeThresholdMs)
        {
            CreateAlert(
                new PaginationAlert
                {
                    Type = AlertType.Performance,
                    Severity = AlertSeverity.High,
                    Title = "Tiempo de ejecución alto",
                    Message =
                        $"Operación {metric.OperationType} tomó {metric.ExecutionTimeMs}ms (umbral: {_config.HighExecutionTimeThresholdMs}ms)",
                    OperationId = metric.OperationId,
                    EntityType = metric.EntityType,
                    ExecutionTimeMs = metric.ExecutionTimeMs,
                    Threshold = _config.HighExecutionTimeThresholdMs,
                }
            );
        }

        // Alerta por tiempo de ejecución crítico
        if (metric.ExecutionTimeMs > _config.CriticalExecutionTimeThresholdMs)
        {
            CreateAlert(
                new PaginationAlert
                {
                    Type = AlertType.Performance,
                    Severity = AlertSeverity.Critical,
                    Title = "Tiempo de ejecución crítico",
                    Message =
                        $"Operación {metric.OperationType} tomó {metric.ExecutionTimeMs}ms (umbral crítico: {_config.CriticalExecutionTimeThresholdMs}ms)",
                    OperationId = metric.OperationId,
                    EntityType = metric.EntityType,
                    ExecutionTimeMs = metric.ExecutionTimeMs,
                    Threshold = _config.CriticalExecutionTimeThresholdMs,
                }
            );
        }
    }

    /// <summary>
    /// Evalúa alertas de uso
    /// </summary>
    private void EvaluateUsageAlerts(PaginationMetric metric)
    {
        // Alerta por pageSize muy grande
        if (metric.PageSize > _config.LargePageSizeThreshold)
        {
            CreateAlert(
                new PaginationAlert
                {
                    Type = AlertType.Usage,
                    Severity = AlertSeverity.Medium,
                    Title = "PageSize muy grande",
                    Message =
                        $"PageSize {metric.PageSize} excede el umbral recomendado de {_config.LargePageSizeThreshold}",
                    OperationId = metric.OperationId,
                    EntityType = metric.EntityType,
                    PageSize = metric.PageSize,
                    Threshold = _config.LargePageSizeThreshold,
                }
            );
        }

        // Alerta por página muy alta
        if (metric.Page > _config.HighPageThreshold)
        {
            CreateAlert(
                new PaginationAlert
                {
                    Type = AlertType.Usage,
                    Severity = AlertSeverity.Low,
                    Title = "Página muy alta",
                    Message =
                        $"Acceso a página {metric.Page} (umbral: {_config.HighPageThreshold})",
                    OperationId = metric.OperationId,
                    EntityType = metric.EntityType,
                    Page = metric.Page,
                    Threshold = _config.HighPageThreshold,
                }
            );
        }
    }

    /// <summary>
    /// Evalúa alertas de errores
    /// </summary>
    private void EvaluateErrorAlerts(PaginationMetric metric)
    {
        if (!metric.IsSuccess && !string.IsNullOrEmpty(metric.ErrorMessage))
        {
            CreateAlert(
                new PaginationAlert
                {
                    Type = AlertType.Error,
                    Severity = AlertSeverity.High,
                    Title = "Error en paginación",
                    Message = $"Error en operación {metric.OperationType}: {metric.ErrorMessage}",
                    OperationId = metric.OperationId,
                    EntityType = metric.EntityType,
                    ErrorMessage = metric.ErrorMessage,
                }
            );
        }
    }

    /// <summary>
    /// Evalúa alertas de concurrencia
    /// </summary>
    private void EvaluateConcurrencyAlerts(PaginationMetric metric)
    {
        // Esta es una implementación simplificada
        // En producción, esto evaluaría métricas de concurrencia reales
    }

    /// <summary>
    /// Crea una alerta
    /// </summary>
    private void CreateAlert(PaginationAlert alert)
    {
        alert.Id = Guid.NewGuid();
        alert.CreatedAt = DateTime.UtcNow;
        alert.Status = AlertStatus.Active;

        _alerts.Add(alert);

        // Limpiar alertas antiguas
        CleanupOldAlerts();

        // Log de la alerta
        _logger?.LogWarning(
            "Alerta de paginación creada: {AlertType} - {Severity} - {Title}",
            alert.Type,
            alert.Severity,
            alert.Title
        );

        // En producción, aquí se enviarían notificaciones (email, Slack, etc.)
        SendAlertNotification(alert);
    }

    /// <summary>
    /// Envía notificación de alerta
    /// </summary>
    private void SendAlertNotification(PaginationAlert alert)
    {
        // Implementación simplificada - en producción esto enviaría notificaciones reales
        _logger?.LogInformation(
            "Notificación de alerta enviada: {AlertId} - {Title}",
            alert.Id,
            alert.Title
        );
    }

    /// <summary>
    /// Obtiene alertas activas
    /// </summary>
    public List<PaginationAlert> GetActiveAlerts(TimeSpan? timeWindow = null)
    {
        var cutoffTime = timeWindow.HasValue
            ? DateTime.UtcNow - timeWindow.Value
            : DateTime.UtcNow - TimeSpan.FromHours(24);

        return _alerts
            .Where(a => a.Status == AlertStatus.Active && a.CreatedAt >= cutoffTime)
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// Obtiene alertas por tipo
    /// </summary>
    public List<PaginationAlert> GetAlertsByType(AlertType type, TimeSpan? timeWindow = null)
    {
        var cutoffTime = timeWindow.HasValue
            ? DateTime.UtcNow - timeWindow.Value
            : DateTime.UtcNow - TimeSpan.FromHours(24);

        return _alerts
            .Where(a => a.Type == type && a.CreatedAt >= cutoffTime)
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// Obtiene alertas por severidad
    /// </summary>
    public List<PaginationAlert> GetAlertsBySeverity(
        AlertSeverity severity,
        TimeSpan? timeWindow = null
    )
    {
        var cutoffTime = timeWindow.HasValue
            ? DateTime.UtcNow - timeWindow.Value
            : DateTime.UtcNow - TimeSpan.FromHours(24);

        return _alerts
            .Where(a => a.Severity == severity && a.CreatedAt >= cutoffTime)
            .OrderByDescending(a => a.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// Resuelve una alerta
    /// </summary>
    public void ResolveAlert(Guid alertId, string? resolution = null)
    {
        var alert = _alerts.FirstOrDefault(a => a.Id == alertId);
        if (alert != null)
        {
            alert.Status = AlertStatus.Resolved;
            alert.ResolvedAt = DateTime.UtcNow;
            alert.Resolution = resolution;

            _logger?.LogInformation("Alerta resuelta: {AlertId} - {Title}", alertId, alert.Title);
        }
    }

    /// <summary>
    /// Obtiene estadísticas de alertas
    /// </summary>
    public AlertStatistics GetAlertStatistics(TimeSpan? timeWindow = null)
    {
        var cutoffTime = timeWindow.HasValue
            ? DateTime.UtcNow - timeWindow.Value
            : DateTime.UtcNow - TimeSpan.FromHours(24);

        var relevantAlerts = _alerts.Where(a => a.CreatedAt >= cutoffTime).ToList();

        return new AlertStatistics
        {
            TotalAlerts = relevantAlerts.Count,
            ActiveAlerts = relevantAlerts.Count(a => a.Status == AlertStatus.Active),
            ResolvedAlerts = relevantAlerts.Count(a => a.Status == AlertStatus.Resolved),
            CriticalAlerts = relevantAlerts.Count(a => a.Severity == AlertSeverity.Critical),
            HighAlerts = relevantAlerts.Count(a => a.Severity == AlertSeverity.High),
            MediumAlerts = relevantAlerts.Count(a => a.Severity == AlertSeverity.Medium),
            LowAlerts = relevantAlerts.Count(a => a.Severity == AlertSeverity.Low),
            PerformanceAlerts = relevantAlerts.Count(a => a.Type == AlertType.Performance),
            UsageAlerts = relevantAlerts.Count(a => a.Type == AlertType.Usage),
            ErrorAlerts = relevantAlerts.Count(a => a.Type == AlertType.Error),
            ConcurrencyAlerts = relevantAlerts.Count(a => a.Type == AlertType.Concurrency),
            TimeWindow = timeWindow ?? TimeSpan.FromHours(24),
        };
    }

    /// <summary>
    /// Limpia alertas antiguas
    /// </summary>
    private void CleanupOldAlerts()
    {
        var cutoffTime = DateTime.UtcNow - _config.AlertRetentionPeriod;
        var removedCount = _alerts.RemoveAll(a => a.CreatedAt < cutoffTime);

        if (removedCount > 0)
        {
            _logger?.LogInformation("Limpiadas {Count} alertas antiguas", removedCount);
        }
    }

    /// <summary>
    /// Exporta alertas a JSON
    /// </summary>
    public string ExportAlertsToJson(TimeSpan? timeWindow = null)
    {
        var cutoffTime = timeWindow.HasValue
            ? DateTime.UtcNow - timeWindow.Value
            : DateTime.UtcNow - TimeSpan.FromHours(24);

        var relevantAlerts = _alerts
            .Where(a => a.CreatedAt >= cutoffTime)
            .Select(a => new
            {
                a.Id,
                a.Type,
                a.Severity,
                a.Status,
                a.Title,
                a.Message,
                a.OperationId,
                a.EntityType,
                a.ExecutionTimeMs,
                a.Page,
                a.PageSize,
                a.Threshold,
                a.ErrorMessage,
                a.CreatedAt,
                a.ResolvedAt,
                a.Resolution,
            })
            .ToList();

        return System.Text.Json.JsonSerializer.Serialize(
            relevantAlerts,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
        );
    }
}

/// <summary>
/// Alerta de paginación
/// </summary>
public class PaginationAlert
{
    public Guid Id { get; set; }
    public AlertType Type { get; set; }
    public AlertSeverity Severity { get; set; }
    public AlertStatus Status { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string OperationId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public long? ExecutionTimeMs { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
    public double? Threshold { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Resolution { get; set; }
}

/// <summary>
/// Tipo de alerta
/// </summary>
public enum AlertType
{
    Performance,
    Usage,
    Error,
    Concurrency,
}

/// <summary>
/// Severidad de alerta
/// </summary>
public enum AlertSeverity
{
    Low,
    Medium,
    High,
    Critical,
}

/// <summary>
/// Estado de alerta
/// </summary>
public enum AlertStatus
{
    Active,
    Resolved,
    Suppressed,
}

/// <summary>
/// Estadísticas de alertas
/// </summary>
public class AlertStatistics
{
    public int TotalAlerts { get; set; }
    public int ActiveAlerts { get; set; }
    public int ResolvedAlerts { get; set; }
    public int CriticalAlerts { get; set; }
    public int HighAlerts { get; set; }
    public int MediumAlerts { get; set; }
    public int LowAlerts { get; set; }
    public int PerformanceAlerts { get; set; }
    public int UsageAlerts { get; set; }
    public int ErrorAlerts { get; set; }
    public int ConcurrencyAlerts { get; set; }
    public TimeSpan TimeWindow { get; set; }
}

/// <summary>
/// Configuración de alertas
/// </summary>
public class PaginationAlertConfiguration
{
    public long HighExecutionTimeThresholdMs { get; set; } = 1000;
    public long CriticalExecutionTimeThresholdMs { get; set; } = 5000;
    public int LargePageSizeThreshold { get; set; } = 100;
    public int HighPageThreshold { get; set; } = 1000;
    public TimeSpan AlertRetentionPeriod { get; set; } = TimeSpan.FromDays(30);
    public bool EnableAlerts { get; set; } = true;
    public bool EnableNotifications { get; set; } = true;
}
