using System.Text.Json;
using Cronos;
using GestionHogar.Controllers;
using GestionHogar.Controllers.Notifications.Dto;
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

    // Rate limiting para notificaciones
    private const int MAX_NOTIFICATIONS_PER_BATCH = 50; // Máximo 50 notificaciones por lote
    private const int MAX_NOTIFICATIONS_PER_USER = 10; // Máximo 10 leads por usuario
    private const int NOTIFICATION_COOLDOWN_HOURS = 1; // 1 hora de cooldown
    private const bool ENABLE_NOTIFICATION_GROUPING = true; // Agrupar notificaciones
    private const int MAX_LEADS_PER_NOTIFICATION = 5; // Máximo 5 leads por notificación individual
    private const int PRIORITY_LEAD_DAYS_OLD = 3; // Leads más antiguos tienen prioridad

    // Clase para resultado del procesamiento
    private record ProcessResult(List<Lead> Processed, List<Lead> Deferred);

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
        var cronSchedule = configuration["LeadExpiration:CronSchedule"] ?? "0 0 0,8,16 * * *";
        _logger.LogInformation("🔧 Configurando cron schedule: '{CronSchedule}'", cronSchedule);
        _cronExpression = CronExpression.Parse(cronSchedule, CronFormat.IncludeSeconds);

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

                // Enviar notificaciones
                using var scope = _serviceProvider.CreateScope();
                await SendExpirationNotifications(leads, scope.ServiceProvider);
            }

            return processedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error al procesar lote de {Count} leads", leads.Count);
            throw;
        }
    }

    /// <summary>
    /// Envía notificaciones de leads expirados con rate limiting robusto
    /// </summary>
    private async Task SendExpirationNotifications(
        List<Lead> expiredLeads,
        IServiceProvider serviceProvider
    )
    {
        if (expiredLeads.Count == 0)
            return;

        try
        {
            _logger.LogInformation(
                "Procesando notificaciones para {Count} leads expirados",
                expiredLeads.Count
            );

            // 1. PRIORIZAR LEADS: Los más antiguos primero
            var prioritizedLeads = expiredLeads
                .OrderBy(l => l.ExpirationDate)
                .ThenBy(l => l.CreatedAt)
                .ToList();

            // 2. AGRUPAR POR USUARIO
            var leadsByUser = prioritizedLeads
                .Where(l => l.AssignedToId.HasValue)
                .GroupBy(l => l.AssignedToId!.Value);
            var totalNotificationsSent = 0;
            var totalLeadsProcessed = 0;
            var leadsDeferred = new List<Lead>();

            foreach (var userGroup in leadsByUser)
            {
                var userId = userGroup.Key;
                var userLeads = userGroup.ToList();
                var totalUserLeads = userLeads.Count;

                _logger.LogDebug(
                    "Procesando usuario {UserId} con {Count} leads expirados",
                    userId,
                    totalUserLeads
                );

                // 3. VERIFICAR COOLDOWN
                if (await HasRecentExpirationNotification(userId, serviceProvider))
                {
                    _logger.LogDebug(
                        "Usuario {UserId} tiene notificación reciente, diferiendo {Count} leads",
                        userId,
                        totalUserLeads
                    );
                    leadsDeferred.AddRange(userLeads);
                    continue;
                }

                // 4. PROCESAR LEADS DEL USUARIO CON ESTRATEGIA INTELIGENTE
                var result = await ProcessUserLeadsIntelligently(
                    userId,
                    userLeads,
                    serviceProvider
                );

                totalLeadsProcessed += result.Processed.Count;
                leadsDeferred.AddRange(result.Deferred);
                totalNotificationsSent += result.Processed.Count > 0 ? 1 : 0;
            }

            // 5. MANEJAR LEADS DIFERIDOS
            if (leadsDeferred.Count > 0)
            {
                _logger.LogInformation(
                    "Diferidos {Count} leads para próxima verificación",
                    leadsDeferred.Count
                );
                await MarkLeadsForDeferredNotification(leadsDeferred, serviceProvider);
            }

            _logger.LogInformation(
                "Notificaciones completadas: {Processed} leads procesados, {Sent} notificaciones enviadas, {Deferred} diferidos",
                totalLeadsProcessed,
                totalNotificationsSent,
                leadsDeferred.Count
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando notificaciones de expiración");
        }
    }

    /// <summary>
    /// Procesa leads de un usuario con estrategia inteligente
    /// </summary>
    private async Task<ProcessResult> ProcessUserLeadsIntelligently(
        Guid userId,
        List<Lead> userLeads,
        IServiceProvider serviceProvider
    )
    {
        var processed = new List<Lead>();
        var deferred = new List<Lead>();

        // Estrategia 1: Si hay pocos leads, procesar todos
        if (userLeads.Count <= MAX_NOTIFICATIONS_PER_USER)
        {
            await SendGroupedNotificationForUser(
                userId,
                userLeads,
                userLeads.Count,
                serviceProvider
            );
            processed.AddRange(userLeads);
            return new ProcessResult(processed, deferred);
        }

        // Estrategia 2: Muchos leads - dividir en notificaciones más pequeñas
        var leadsToProcess = userLeads.Take(MAX_NOTIFICATIONS_PER_USER).ToList();
        var leadsToDefer = userLeads.Skip(MAX_NOTIFICATIONS_PER_USER).ToList();

        // Procesar los leads prioritarios
        await SendGroupedNotificationForUser(
            userId,
            leadsToProcess,
            userLeads.Count,
            serviceProvider
        );
        processed.AddRange(leadsToProcess);

        // Los restantes se difieren
        deferred.AddRange(leadsToDefer);

        _logger.LogInformation(
            "Usuario {UserId}: {Processed} leads procesados, {Deferred} diferidos",
            userId,
            processed.Count,
            deferred.Count
        );

        return new ProcessResult(processed, deferred);
    }

    /// <summary>
    /// Marca leads para notificación diferida
    /// </summary>
    private async Task MarkLeadsForDeferredNotification(
        List<Lead> leads,
        IServiceProvider serviceProvider
    )
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        try
        {
            // Crear notificación de sistema para recordar leads diferidos
            var deferredNotification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = Guid.Empty, // Notificación de sistema
                Type = NotificationType.SystemAlert,
                Priority = NotificationPriority.Normal,
                Channel = NotificationChannel.InApp,
                Title = "Leads Diferidos",
                Message =
                    $"{leads.Count} leads expirados fueron diferidos para próxima verificación",
                Data = JsonSerializer.Serialize(
                    new
                    {
                        DeferredLeads = leads
                            .Select(l => new
                            {
                                l.Id,
                                l.Code,
                                l.ExpirationDate,
                            })
                            .ToList(),
                        DeferredAt = DateTime.UtcNow,
                        Reason = "Rate limiting exceeded",
                    }
                ),
                IsRead = false,
                ReadAt = null,
                SentAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(1),
                RelatedEntityType = "System",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
            };

            context.Notifications.Add(deferredNotification);
            await context.SaveChangesAsync();

            _logger.LogInformation(
                "Creada notificación de sistema para {Count} leads diferidos",
                leads.Count
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marcando leads para notificación diferida");
        }
    }

    /// <summary>
    /// Verifica si el usuario tiene una notificación reciente de expiración
    /// </summary>
    private async Task<bool> HasRecentExpirationNotification(
        Guid userId,
        IServiceProvider serviceProvider
    )
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var cooldownTime = DateTime.UtcNow.AddHours(-NOTIFICATION_COOLDOWN_HOURS);

        return await context.Notifications.AnyAsync(n =>
            n.UserId == userId
            && n.Type == NotificationType.LeadExpired
            && n.CreatedAt > cooldownTime
            && !n.IsRead
        );
    }

    /// <summary>
    /// Envía notificación agrupada para un usuario siguiendo el modelo Notification
    /// </summary>
    private async Task SendGroupedNotificationForUser(
        Guid userId,
        List<Lead> userLeads,
        int totalUserLeads,
        IServiceProvider serviceProvider
    )
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        // Crear notificación siguiendo el modelo
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = NotificationType.LeadExpired,
            Priority = NotificationPriority.High,
            Channel = NotificationChannel.InApp,
            Title = totalUserLeads == 1 ? "Lead Expirado" : "Leads Expirados",
            Message =
                totalUserLeads == 1
                    ? $"El lead '{userLeads.First().Code}' ha expirado"
                    : $"{totalUserLeads} leads han expirado",
            Data = JsonSerializer.Serialize(
                new
                {
                    ExpiredCount = totalUserLeads,
                    LeadIds = userLeads.Select(l => l.Id).ToList(),
                    ExpiredAt = DateTime.UtcNow,
                }
            ),
            IsRead = false,
            ReadAt = null,
            SentAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7), // Expira en 7 días
            RelatedEntityId = totalUserLeads == 1 ? userLeads.First().Id : null,
            RelatedEntityType = "Lead",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
        };

        // Guardar en base de datos
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        // Enviar via SSE
        await EnqueueNotificationForUser(userId, notification, serviceProvider);
    }

    /// <summary>
    /// Encola notificación para envío via SSE
    /// </summary>
    private async Task EnqueueNotificationForUser(
        Guid userId,
        Notification notification,
        IServiceProvider serviceProvider
    )
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var notificationStreamController =
                scope.ServiceProvider.GetRequiredService<NotificationStreamController>();

            // Crear DTO para SSE
            var notificationDto = new NotificationDto
            {
                Id = notification.Id,
                UserId = notification.UserId,
                UserName = "Sistema",
                Type = notification.Type,
                Priority = notification.Priority,
                Channel = notification.Channel,
                Title = notification.Title,
                Message = notification.Message,
                Data = notification.Data,
                IsRead = notification.IsRead,
                ReadAt = notification.ReadAt,
                SentAt = notification.SentAt,
                ExpiresAt = notification.ExpiresAt,
                RelatedEntityId = notification.RelatedEntityId,
                RelatedEntityType = notification.RelatedEntityType,
                CreatedAt = notification.CreatedAt,
                ModifiedAt = notification.ModifiedAt,
            };

            // Enviar via SSE
            NotificationStreamController.EnqueueNotificationForUser(userId, notificationDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error enviando notificación via SSE para usuario {UserId}",
                userId
            );
        }
    }

    public override void Dispose()
    {
        _semaphore?.Dispose();
        base.Dispose();
    }
}
