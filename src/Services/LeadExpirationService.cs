using Cronos;
using GestionHogar.Controllers;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestionHogar.Services;

/// <summary>
/// Servicio para expiración automática de leads con validaciones robustas
/// </summary>
public class LeadExpirationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LeadExpirationService> _logger;
    private readonly CronExpression _cronExpression;
    private readonly IConfiguration _configuration;
    private readonly SemaphoreSlim _semaphore = new(1, 1); // Evita ejecuciones concurrentes
    private readonly TimeSpan _executionTimeout = TimeSpan.FromMinutes(5); // Timeout de ejecución
    private int _consecutiveErrors = 0; // Contador de errores consecutivos
    private readonly int _maxConsecutiveErrors; // Máximo de errores consecutivos antes de backoff
    private readonly int _initialBackoffMinutes; // Delay inicial para backoff
    private readonly int _maxBackoffMinutes; // Delay máximo para backoff
    private DateTime? _lastErrorTime = null; // Tiempo del último error
    private TimeSpan _backoffDelay; // Delay actual para backoff

    public LeadExpirationService(
        IServiceProvider serviceProvider,
        ILogger<LeadExpirationService> logger,
        IConfiguration configuration
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;

        // Configuración del cron schedule
        var cronSchedule = configuration["LeadExpiration:CronSchedule"] ?? "0 0 */8 * * *";
        _cronExpression = CronExpression.Parse(cronSchedule);

        // Configuración de backoff
        _maxConsecutiveErrors = configuration.GetValue<int>(
            "LeadExpiration:MaxConsecutiveErrors",
            3
        );
        _initialBackoffMinutes = configuration.GetValue<int>(
            "LeadExpiration:InitialBackoffMinutes",
            30
        );
        _maxBackoffMinutes = configuration.GetValue<int>("LeadExpiration:MaxBackoffMinutes", 1440);
        _backoffDelay = TimeSpan.FromMinutes(_initialBackoffMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue<bool>("LeadExpiration:Enabled", true);
        if (!enabled)
        {
            _logger.LogInformation("🚫 LeadExpirationService deshabilitado por configuración");
            return;
        }

        _logger.LogInformation(
            "🚀 LeadExpirationService iniciado - Horario: {CronSchedule}",
            _cronExpression.ToString()
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var nextRun = _cronExpression.GetNextOccurrence(now);

                if (nextRun.HasValue)
                {
                    var delay = nextRun.Value - now;
                    _logger.LogInformation(
                        "⏰ Próxima verificación: {NextRun} (en {Delay})",
                        nextRun.Value,
                        delay.ToString(@"hh\:mm\:ss")
                    );

                    await Task.Delay(delay, stoppingToken);
                }

                if (!stoppingToken.IsCancellationRequested)
                {
                    // Verificar si estamos en modo backoff
                    if (_consecutiveErrors >= _maxConsecutiveErrors && _lastErrorTime.HasValue)
                    {
                        var timeSinceLastError = DateTime.UtcNow - _lastErrorTime.Value;
                        if (timeSinceLastError < _backoffDelay)
                        {
                            var remainingTime = _backoffDelay - timeSinceLastError;
                            _logger.LogInformation(
                                "⏳ Modo backoff activo. Reintentando en {RemainingTime}",
                                remainingTime.ToString(@"hh\:mm\:ss")
                            );
                            await Task.Delay(remainingTime, stoppingToken);
                        }
                        else
                        {
                            _logger.LogInformation(
                                "🔄 Reintentando después del período de backoff"
                            );
                        }
                    }

                    var success = await CheckExpiredLeadsSafely();
                    if (success)
                    {
                        // Éxito: resetear todo
                        _consecutiveErrors = 0;
                        _lastErrorTime = null;
                        _backoffDelay = TimeSpan.FromMinutes(_initialBackoffMinutes); // Resetear delay
                        _logger.LogInformation(
                            "✅ Verificación exitosa. Contador de errores reseteado"
                        );
                    }
                    else
                    {
                        _consecutiveErrors++;
                        _lastErrorTime = DateTime.UtcNow;

                        _logger.LogWarning(
                            "⚠️ Error en verificación de leads. Errores consecutivos: {Count}/{Max}",
                            _consecutiveErrors,
                            _maxConsecutiveErrors
                        );

                        if (_consecutiveErrors >= _maxConsecutiveErrors)
                        {
                            // Aumentar el delay exponencialmente (máximo configurado)
                            var exponentialDelay =
                                _initialBackoffMinutes
                                * Math.Pow(2, _consecutiveErrors - _maxConsecutiveErrors);
                            _backoffDelay = TimeSpan.FromMinutes(
                                Math.Min(exponentialDelay, _maxBackoffMinutes)
                            );

                            _logger.LogError(
                                "🛑 Demasiados errores consecutivos ({Count}). Entrando en modo backoff por {BackoffTime}",
                                _consecutiveErrors,
                                _backoffDelay.ToString(@"hh\:mm\:ss")
                            );
                            // No hacer break, continuar con el siguiente ciclo
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("🛑 LeadExpirationService cancelado");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "❌ Error crítico en LeadExpirationService - Deteniendo servicio"
                );
                // En caso de error crítico, detener el servicio para evitar bucles infinitos
                break;
            }
        }
    }

    /// <summary>
    /// Ejecuta la verificación de leads expirados de forma segura
    /// </summary>
    /// <returns>True si la ejecución fue exitosa, False si hubo errores</returns>
    private async Task<bool> CheckExpiredLeadsSafely()
    {
        // Evitar ejecuciones concurrentes con timeout
        if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(10)))
        {
            _logger.LogWarning("⚠️ Verificación anterior aún en progreso, saltando esta ejecución");
            return false;
        }

        try
        {
            using var cts = new CancellationTokenSource(_executionTimeout);
            await CheckExpiredLeads(cts.Token);
            return true; // Éxito
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("⏱️ Verificación de leads expirados cancelada por timeout");
            return false; // Error por timeout
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error en CheckExpiredLeadsSafely");
            return false; // Error general
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Verifica y expira leads de forma optimizada
    /// </summary>
    private async Task CheckExpiredLeads(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var startTime = DateTime.UtcNow;
        var processedCount = 0;
        var errorCount = 0;

        try
        {
            _logger.LogInformation("🔍 Iniciando verificación optimizada de leads expirados...");

            // Validación previa: verificar si hay leads que puedan expirar
            var potentialExpiredCount = await GetPotentialExpiredLeadsCount(
                context,
                cancellationToken
            );

            if (potentialExpiredCount == 0)
            {
                _logger.LogInformation("ℹ️ No se encontraron leads candidatos para expirar");
                return;
            }

            _logger.LogInformation(
                "📊 {Count} leads candidatos para expiración encontrados",
                potentialExpiredCount
            );

            // Procesar leads en lotes para evitar problemas de memoria
            const int batchSize = 100;
            var totalBatches = (int)Math.Ceiling((double)potentialExpiredCount / batchSize);

            for (int batch = 0; batch < totalBatches; batch++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var expiredLeads = await GetExpiredLeadsBatch(
                    context,
                    batch,
                    batchSize,
                    cancellationToken
                );

                if (expiredLeads.Count == 0)
                    break;

                var batchProcessed = await ProcessLeadsBatch(
                    context,
                    expiredLeads,
                    cancellationToken
                );
                processedCount += batchProcessed;

                _logger.LogInformation(
                    "📦 Lote {Current}/{Total} procesado: {Processed} leads expirados",
                    batch + 1,
                    totalBatches,
                    batchProcessed
                );

                // Pequeña pausa entre lotes para no sobrecargar la DB
                await Task.Delay(100, cancellationToken);
            }

            var duration = DateTime.UtcNow - startTime;

            if (processedCount > 0)
            {
                _logger.LogInformation(
                    "✅ Proceso completado: {Processed} leads expirados en {Duration}ms",
                    processedCount,
                    duration.TotalMilliseconds
                );
            }
            else
            {
                _logger.LogInformation(
                    "ℹ️ No se procesaron leads (duración: {Duration}ms)",
                    duration.TotalMilliseconds
                );
            }
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "💾 Error de base de datos al procesar leads expirados");
            errorCount++;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error inesperado al verificar leads expirados");
            errorCount++;
        }
        finally
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation(
                "📈 Resumen: {Processed} procesados, {Errors} errores, {Duration}ms",
                processedCount,
                errorCount,
                duration.TotalMilliseconds
            );
        }
    }

    /// <summary>
    /// Obtiene el conteo de leads que potencialmente pueden expirar (optimización)
    /// </summary>
    private async Task<int> GetPotentialExpiredLeadsCount(
        DatabaseContext context,
        CancellationToken cancellationToken
    )
    {
        var now = DateTime.UtcNow;

        return await context
            .Leads.Where(l =>
                l.IsActive
                && l.ExpirationDate < now
                && l.Status != LeadStatus.Expired
                && l.Status != LeadStatus.Completed
                && l.Status != LeadStatus.Canceled
            )
            .CountAsync(cancellationToken);
    }

    /// <summary>
    /// Obtiene un lote de leads expirados
    /// </summary>
    private async Task<List<Lead>> GetExpiredLeadsBatch(
        DatabaseContext context,
        int batch,
        int batchSize,
        CancellationToken cancellationToken
    )
    {
        var now = DateTime.UtcNow;

        return await context
            .Leads.Where(l =>
                l.IsActive
                && l.ExpirationDate < now
                && l.Status != LeadStatus.Expired
                && l.Status != LeadStatus.Completed
                && l.Status != LeadStatus.Canceled
            )
            .OrderBy(l => l.ExpirationDate) // Ordenar por fecha de expiración
            .Skip(batch * batchSize)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Procesa un lote de leads y los marca como expirados
    /// </summary>
    private async Task<int> ProcessLeadsBatch(
        DatabaseContext context,
        List<Lead> leads,
        CancellationToken cancellationToken
    )
    {
        if (leads.Count == 0)
            return 0;

        var now = DateTime.UtcNow;
        var processedCount = 0;

        try
        {
            // Actualizar todos los leads del lote en una sola operación
            foreach (var lead in leads)
            {
                // Validación adicional antes de marcar como expirado
                if (
                    lead.ExpirationDate < now
                    && lead.Status != LeadStatus.Expired
                    && lead.Status != LeadStatus.Completed
                    && lead.Status != LeadStatus.Canceled
                )
                {
                    lead.Status = LeadStatus.Expired;
                    lead.ModifiedAt = now;
                    processedCount++;
                }
            }

            if (processedCount > 0)
            {
                await context.SaveChangesAsync(cancellationToken);
                _logger.LogDebug("💾 Lote guardado: {Count} leads actualizados", processedCount);
            }

            return processedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error al procesar lote de {Count} leads", leads.Count);
            throw;
        }
    }

    public override void Dispose()
    {
        _semaphore?.Dispose();
        base.Dispose();
    }
}
