using GestionHogar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionHogar.Controllers;

/// <summary>
/// Controlador para métricas y monitoreo de paginación
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaginationMetricsController : ControllerBase
{
    private readonly PaginationMetricsService _metricsService;
    private readonly ILogger<PaginationMetricsController> _logger;

    public PaginationMetricsController(
        PaginationMetricsService metricsService,
        ILogger<PaginationMetricsController> logger
    )
    {
        _metricsService = metricsService;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene reporte de performance de paginación
    /// </summary>
    [HttpGet("performance-report")]
    [ProducesResponseType(typeof(PaginationPerformanceReport), StatusCodes.Status200OK)]
    public ActionResult<PaginationPerformanceReport> GetPerformanceReport(
        [FromQuery] int? hours = null
    )
    {
        try
        {
            var timeWindow = hours.HasValue
                ? TimeSpan.FromHours(hours.Value)
                : TimeSpan.FromHours(24);
            var report = _metricsService.GetPerformanceReport(timeWindow);

            _logger.LogInformation(
                "Reporte de performance generado: {TotalOperations} operaciones en {TimeWindow}",
                report.TotalOperations,
                timeWindow
            );

            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando reporte de performance");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene métricas por tipo de operación
    /// </summary>
    [HttpGet("metrics-by-operation")]
    [ProducesResponseType(typeof(Dictionary<string, OperationMetrics>), StatusCodes.Status200OK)]
    public ActionResult<Dictionary<string, OperationMetrics>> GetMetricsByOperationType(
        [FromQuery] int? hours = null
    )
    {
        try
        {
            var timeWindow = hours.HasValue
                ? TimeSpan.FromHours(hours.Value)
                : TimeSpan.FromHours(24);
            var metrics = _metricsService.GetMetricsByOperationType(timeWindow);

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo métricas por tipo de operación");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene métricas por entidad
    /// </summary>
    [HttpGet("metrics-by-entity")]
    [ProducesResponseType(typeof(Dictionary<string, EntityMetrics>), StatusCodes.Status200OK)]
    public ActionResult<Dictionary<string, EntityMetrics>> GetMetricsByEntity(
        [FromQuery] int? hours = null
    )
    {
        try
        {
            var timeWindow = hours.HasValue
                ? TimeSpan.FromHours(hours.Value)
                : TimeSpan.FromHours(24);
            var metrics = _metricsService.GetMetricsByEntity(timeWindow);

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo métricas por entidad");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Detecta operaciones lentas
    /// </summary>
    [HttpGet("slow-operations")]
    [ProducesResponseType(typeof(List<PaginationMetric>), StatusCodes.Status200OK)]
    public ActionResult<List<PaginationMetric>> GetSlowOperations(
        [FromQuery] double thresholdMultiplier = 2.0,
        [FromQuery] int? hours = null
    )
    {
        try
        {
            var timeWindow = hours.HasValue
                ? TimeSpan.FromHours(hours.Value)
                : TimeSpan.FromHours(24);
            var slowOperations = _metricsService.DetectSlowOperations(
                thresholdMultiplier,
                timeWindow
            );

            _logger.LogInformation(
                "Detectadas {Count} operaciones lentas con threshold {Threshold}",
                slowOperations.Count,
                thresholdMultiplier
            );

            return Ok(slowOperations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detectando operaciones lentas");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene recomendaciones de optimización
    /// </summary>
    [HttpGet("optimization-recommendations")]
    [ProducesResponseType(typeof(List<OptimizationRecommendation>), StatusCodes.Status200OK)]
    public ActionResult<List<OptimizationRecommendation>> GetOptimizationRecommendations()
    {
        try
        {
            var recommendations = _metricsService.GetOptimizationRecommendations();

            _logger.LogInformation(
                "Generadas {Count} recomendaciones de optimización",
                recommendations.Count
            );

            return Ok(recommendations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando recomendaciones de optimización");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Exporta métricas a JSON
    /// </summary>
    [HttpGet("export-metrics")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public ActionResult<string> ExportMetrics([FromQuery] int? hours = null)
    {
        try
        {
            var timeWindow = hours.HasValue
                ? TimeSpan.FromHours(hours.Value)
                : TimeSpan.FromHours(24);
            var jsonData = _metricsService.ExportMetricsToJson(timeWindow);

            _logger.LogInformation(
                "Métricas exportadas para ventana de tiempo: {TimeWindow}",
                timeWindow
            );

            return Ok(jsonData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exportando métricas");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Limpia métricas antiguas
    /// </summary>
    [HttpPost("cleanup-old-metrics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult CleanupOldMetrics([FromQuery] int retentionHours = 168) // 7 días por defecto
    {
        try
        {
            var retentionPeriod = TimeSpan.FromHours(retentionHours);
            _metricsService.CleanupOldMetrics(retentionPeriod);

            _logger.LogInformation(
                "Limpieza de métricas completada: retención {RetentionPeriod}",
                retentionPeriod
            );

            return Ok(new { message = "Limpieza de métricas completada exitosamente" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error limpiando métricas antiguas");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene estadísticas de salud del sistema de paginación
    /// </summary>
    [HttpGet("health-status")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult<object> GetHealthStatus()
    {
        try
        {
            var report = _metricsService.GetPerformanceReport(TimeSpan.FromHours(1));
            var recommendations = _metricsService.GetOptimizationRecommendations();

            var healthStatus = new
            {
                Status = report.PerformanceScore > 80 ? "Healthy"
                : report.PerformanceScore > 60 ? "Warning"
                : "Critical",
                PerformanceScore = report.PerformanceScore,
                TotalOperationsLastHour = report.TotalOperations,
                AverageExecutionTimeMs = report.AverageExecutionTimeMs,
                SlowOperationsCount = report.SlowOperationsCount,
                RecommendationsCount = recommendations.Count,
                CriticalRecommendations = recommendations.Count(r => r.Priority == "High"),
                LastUpdated = DateTime.UtcNow,
            };

            return Ok(healthStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo estado de salud");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }
}
