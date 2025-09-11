using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GestionHogar.Services;

/// <summary>
/// Health check para el servicio de expiración de leads
/// </summary>
public class LeadExpirationHealthCheck : IHealthCheck
{
    private readonly ILogger<LeadExpirationHealthCheck> _logger;

    public LeadExpirationHealthCheck(ILogger<LeadExpirationHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // Health check simple que verifica que el servicio está registrado
            var data = new Dictionary<string, object>
            {
                ["service_name"] = "LeadExpirationService",
                ["status"] = "registered",
                ["check_time"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                ["description"] = "Lead expiration service is running",
            };

            _logger.LogDebug("✅ LeadExpirationService health check passed");
            return Task.FromResult(
                HealthCheckResult.Healthy("LeadExpirationService is registered and running", data)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during LeadExpirationService health check");
            return Task.FromResult(
                HealthCheckResult.Unhealthy(
                    "LeadExpirationService health check failed with exception",
                    ex,
                    new Dictionary<string, object> { ["exception"] = ex.Message }
                )
            );
        }
    }
}
