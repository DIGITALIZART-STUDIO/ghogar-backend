using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using GestionHogar.Controllers.Notifications.Dto;
using GestionHogar.Model;
using GestionHogar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionHogar.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationStreamController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationStreamController> _logger;
    private static readonly ConcurrentDictionary<
        Guid,
        List<NotificationDto>
    > _userNotificationQueues = new();
    private static readonly ConcurrentDictionary<
        Guid,
        TaskCompletionSource<NotificationDto>
    > _userEventSources = new();

    // Método estático para enviar notificaciones inmediatamente via eventos
    public static void EnqueueNotificationForUser(Guid userId, NotificationDto notification)
    {
        // Intentar enviar inmediatamente via evento
        if (_userEventSources.TryGetValue(userId, out var eventSource))
        {
            eventSource.SetResult(notification);

            // Crear nuevo TaskCompletionSource para futuras notificaciones
            _userEventSources[userId] = new TaskCompletionSource<NotificationDto>();
        }
    }

    public NotificationStreamController(
        INotificationService notificationService,
        ILogger<NotificationStreamController> logger
    )
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Endpoint SSE para recibir notificaciones en tiempo real
    /// </summary>
    [HttpGet("stream")]
    public async Task GetNotificationStream()
    {
        // Obtener userId usando el método normal de autenticación por cookies
        var userId = GetCurrentUserId();
        var response = Response;

        _logger.LogInformation("🔵 [SSE] Connection started for user {UserId}", userId);

        // Configurar headers para SSE
        response.Headers["Content-Type"] = "text/event-stream";
        response.Headers["Cache-Control"] = "no-cache";
        response.Headers["Connection"] = "keep-alive";

        // Inicializar evento de notificaciones para el usuario
        _userEventSources[userId] = new TaskCompletionSource<NotificationDto>();
        _userNotificationQueues[userId] = new List<NotificationDto>();

        try
        {
            // Enviar notificación de conexión establecida
            await SendSSEMessage(
                "connection",
                new
                {
                    message = "Conexión establecida",
                    timestamp = DateTime.UtcNow,
                    userId = userId,
                }
            );

            // 🚀 CARGAR NOTIFICACIONES PENDIENTES al conectar
            await LoadAndSendPendingNotifications(userId);

            // Mantener la conexión abierta y esperar eventos de notificaciones
            while (!HttpContext.RequestAborted.IsCancellationRequested)
            {
                try
                {
                    // Verificar que el usuario aún está conectado
                    if (!_userEventSources.TryGetValue(userId, out var eventSource))
                    {
                        _logger.LogWarning(
                            "⚠️ [SSE] User {UserId} event source removed, closing connection",
                            userId
                        );
                        break;
                    }

                    // Esperar por una notificación via evento (sin polling)
                    // Usar CancellationToken para permitir timeout periódico para heartbeat
                    using var cts = new CancellationTokenSource();
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30), cts.Token);
                    var notificationTask = eventSource.Task;

                    var completedTask = await Task.WhenAny(notificationTask, timeoutTask);

                    if (completedTask == notificationTask)
                    {
                        // Notificación recibida
                        cts.Cancel(); // Cancelar timeout
                        var notification = await notificationTask;

                        _logger.LogInformation(
                            "📨 [SSE] Received new notification for user {UserId}: {NotificationId} - {Title}",
                            userId,
                            notification.Id,
                            notification.Title
                        );

                        await SendSSEMessage("notification", notification);

                        // Crear nuevo TaskCompletionSource para la siguiente notificación
                        if (_userEventSources.TryGetValue(userId, out _))
                        {
                            _userEventSources[userId] = new TaskCompletionSource<NotificationDto>();
                        }
                    }
                    else
                    {
                        // Timeout alcanzado - enviar heartbeat
                        await SendSSEMessage("heartbeat", new { timestamp = DateTime.UtcNow });
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error procesando evento SSE para usuario {UserId}",
                        userId
                    );
                    // Crear nuevo TaskCompletionSource en caso de error si aún existe
                    if (_userEventSources.TryGetValue(userId, out _))
                    {
                        _userEventSources[userId] = new TaskCompletionSource<NotificationDto>();
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("🔴 [SSE] Connection cancelled for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [SSE] Error in SSE stream for user {UserId}", userId);
        }
        finally
        {
            // Limpiar eventos y cola cuando se desconecta
            _userEventSources.TryRemove(userId, out _);
            _userNotificationQueues.TryRemove(userId, out _);
            _logger.LogInformation("🧹 [SSE] Cleaned up resources for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Endpoint para enviar notificación a un usuario específico (solo para administradores)
    /// </summary>
    [HttpPost("send-to-user/{targetUserId}")]
    [Authorize(Roles = "SuperAdmin,Admin,Manager,FinanceManager")]
    public async Task<ActionResult> SendNotificationToUser(
        Guid targetUserId,
        [FromBody] NotificationCreateDto dto
    )
    {
        try
        {
            // Crear la notificación (se encolará automáticamente si usuario está conectado)
            dto.UserId = targetUserId;
            var notification = await _notificationService.CreateNotificationAsync(dto);

            // NO agregar manualmente - NotificationService lo hace automáticamente

            _logger.LogInformation(
                "Notification queued for user {TargetUserId}: {NotificationId}",
                targetUserId,
                notification.Id
            );

            return Ok(
                new
                {
                    message = "Notificación enviada al usuario",
                    notificationId = notification.Id,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification to user {TargetUserId}", targetUserId);
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Endpoint para enviar notificación a múltiples usuarios (solo para administradores)
    /// </summary>
    [HttpPost("send-to-multiple")]
    [Authorize(Roles = "SuperAdmin,Admin,Manager,FinanceManager")]
    public async Task<ActionResult> SendNotificationToMultiple(
        [FromBody] SendToMultipleRequest request
    )
    {
        try
        {
            var results = new List<object>();

            foreach (var userId in request.UserIds)
            {
                var dto = new NotificationCreateDto
                {
                    UserId = userId,
                    Type = request.Type,
                    Priority = request.Priority,
                    Channel = request.Channel,
                    Title = request.Title,
                    Message = request.Message,
                    Data = request.Data,
                    ExpiresAt = request.ExpiresAt,
                    RelatedEntityId = request.RelatedEntityId,
                    RelatedEntityType = request.RelatedEntityType,
                };

                var notification = await _notificationService.CreateNotificationAsync(dto);

                // NO agregar manualmente - NotificationService lo hace automáticamente

                results.Add(
                    new
                    {
                        userId = userId,
                        notificationId = notification.Id,
                        success = true,
                    }
                );
            }

            _logger.LogInformation("Notifications sent to {Count} users", request.UserIds.Count);

            return Ok(
                new
                {
                    message = $"Notificaciones enviadas a {request.UserIds.Count} usuarios",
                    results = results,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notifications to multiple users");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Obtiene estadísticas de conexiones SSE activas (solo para administradores)
    /// </summary>
    [HttpGet("connection-stats")]
    public ActionResult GetConnectionStats()
    {
        var stats = new
        {
            ActiveConnections = _userNotificationQueues.Count,
            ConnectedUsers = _userNotificationQueues.Keys.ToList(),
            Timestamp = DateTime.UtcNow,
        };

        return Ok(stats);
    }

    /// <summary>
    /// Endpoint de prueba para enviar notificación al usuario actual
    /// </summary>
    [HttpPost("test-notification")]
    public async Task<ActionResult> SendTestNotification()
    {
        try
        {
            var userId = GetCurrentUserId();

            var testNotification = new NotificationCreateDto
            {
                UserId = userId,
                Type = NotificationType.Custom,
                Priority = NotificationPriority.Normal,
                Channel = NotificationChannel.InApp,
                Title = "🧪 Notificación de Prueba",
                Message = $"Esta es una notificación de prueba enviada el {DateTime.Now:HH:mm:ss}",
                Data = JsonSerializer.Serialize(new { test = true, timestamp = DateTime.UtcNow }),
            };

            var notification = await _notificationService.CreateNotificationAsync(testNotification);

            // NO agregar manualmente - NotificationService lo hace automáticamente

            _logger.LogInformation(
                "Test notification sent to user {UserId}: {NotificationId}",
                userId,
                notification.Id
            );

            return Ok(
                new
                {
                    message = "Notificación de prueba enviada",
                    notificationId = notification.Id,
                    userId = userId,
                    timestamp = DateTime.UtcNow,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending test notification");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    private async Task SendSSEMessage(string eventType, object data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data);
            var message = $"event: {eventType}\ndata: {json}\n\n";
            var bytes = System.Text.Encoding.UTF8.GetBytes(message);
            await Response.Body.WriteAsync(bytes, 0, bytes.Length);
            await Response.Body.FlushAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [SSE] Error sending SSE message");
        }
    }

    /// <summary>
    /// Carga y envía notificaciones pendientes no leídas cuando el usuario se conecta
    /// </summary>
    private async Task LoadAndSendPendingNotifications(Guid userId)
    {
        try
        {
            _logger.LogInformation(
                "📋 [LoadPending] Loading pending notifications for user {UserId}",
                userId
            );

            // Cargar notificaciones pendientes no leídas del usuario
            // Límite: últimas 50 notificaciones no leídas (para no saturar el stream)
            var pendingNotifications = await _notificationService.GetUserNotificationsAsync(
                userId,
                page: 1,
                pageSize: 50, // Límite razonable para carga inicial
                isRead: false, // Solo no leídas
                type: null,
                priority: null
            );

            if (pendingNotifications?.Items != null && pendingNotifications.Items.Any())
            {
                var count = pendingNotifications.Items.Count;
                _logger.LogInformation(
                    "📤 [LoadPending] Sending {Count} pending notifications to user {UserId}",
                    count,
                    userId
                );

                // Enviar cada notificación pendiente por SSE
                // Más recientes primero (ya vienen ordenadas por OrderByDescending)
                foreach (var notification in pendingNotifications.Items)
                {
                    await SendSSEMessage("notification", notification);

                    // Pequeño delay para no saturar el cliente
                    await Task.Delay(50, HttpContext.RequestAborted);
                }

                _logger.LogInformation(
                    "✅ [LoadPending] Successfully sent {Count} pending notifications to user {UserId}",
                    count,
                    userId
                );
            }
            else
            {
                _logger.LogInformation(
                    "ℹ️ [LoadPending] No pending notifications for user {UserId}",
                    userId
                );
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Pending notifications loading cancelled for user {UserId}",
                userId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading pending notifications for user {UserId}", userId);
            // No lanzar excepción - continuar con el stream aunque falle la carga inicial
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Usuario no válido");
        }
        return userId;
    }
}
