using System.Security.Claims;
using GestionHogar.Controllers.Notifications.Dto;
using GestionHogar.Model;
using GestionHogar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionHogar.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(
        INotificationService notificationService,
        ILogger<NotificationController> logger
    )
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene las notificaciones del usuario actual
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<NotificationDto>>> GetUserNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? isRead = null,
        [FromQuery] NotificationType? type = null,
        [FromQuery] NotificationPriority? priority = null
    )
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _notificationService.GetUserNotificationsAsync(
                userId,
                page,
                pageSize,
                isRead,
                type,
                priority
            );

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user notifications");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Obtiene una notificación específica por ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<NotificationDto>> GetNotificationById(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var notification = await _notificationService.GetNotificationByIdAsync(id, userId);

            if (notification == null)
            {
                return NotFound("Notificación no encontrada");
            }

            return Ok(notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notification {NotificationId}", id);
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Obtiene estadísticas de notificaciones del usuario
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<NotificationStatsDto>> GetNotificationStats()
    {
        try
        {
            var userId = GetCurrentUserId();
            var stats = await _notificationService.GetUserNotificationStatsAsync(userId);

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notification stats");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Marca una notificación como leída
    /// </summary>
    [HttpPut("{id}/read")]
    public async Task<ActionResult> MarkAsRead(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _notificationService.MarkAsReadAsync(id, userId);

            if (!success)
            {
                return NotFound("Notificación no encontrada");
            }

            return Ok(new { message = "Notificación marcada como leída" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {NotificationId} as read", id);
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Marca una notificación como no leída
    /// </summary>
    [HttpPut("{id}/unread")]
    public async Task<ActionResult> MarkAsUnread(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _notificationService.MarkAsUnreadAsync(id, userId);

            if (!success)
            {
                return NotFound("Notificación no encontrada");
            }

            return Ok(new { message = "Notificación marcada como no leída" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {NotificationId} as unread", id);
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Marca todas las notificaciones del usuario como leídas
    /// </summary>
    [HttpPut("mark-all-read")]
    public async Task<ActionResult> MarkAllAsRead()
    {
        try
        {
            var userId = GetCurrentUserId();
            var count = await _notificationService.MarkAllAsReadAsync(userId);

            return Ok(
                new { message = $"Se marcaron {count} notificaciones como leídas", count = count }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Marca múltiples notificaciones como leídas
    /// </summary>
    [HttpPut("mark-multiple-read")]
    public async Task<ActionResult> MarkMultipleAsRead([FromBody] NotificationBulkUpdateDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var count = await _notificationService.MarkMultipleAsReadAsync(
                dto.NotificationIds,
                userId
            );

            return Ok(
                new { message = $"Se marcaron {count} notificaciones como leídas", count = count }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking multiple notifications as read");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Elimina una notificación
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteNotification(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _notificationService.DeleteNotificationAsync(id, userId);

            if (!success)
            {
                return NotFound("Notificación no encontrada");
            }

            return Ok(new { message = "Notificación eliminada" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification {NotificationId}", id);
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Crea una nueva notificación (solo para administradores)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin,Manager")]
    public async Task<ActionResult<NotificationDto>> CreateNotification(
        [FromBody] NotificationCreateDto dto
    )
    {
        try
        {
            var notification = await _notificationService.CreateNotificationAsync(dto);
            var notificationDto = NotificationDto.FromEntity(notification);

            return CreatedAtAction(
                nameof(GetNotificationById),
                new { id = notification.Id },
                notificationDto
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating notification");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Envía notificaciones pendientes (solo para administradores)
    /// </summary>
    [HttpPost("send-pending")]
    [Authorize(Roles = "SuperAdmin,Admin,Manager")]
    public async Task<ActionResult> SendPendingNotifications()
    {
        try
        {
            var count = await _notificationService.SendPendingNotificationsAsync();

            return Ok(
                new { message = $"Se enviaron {count} notificaciones pendientes", count = count }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending pending notifications");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Limpia notificaciones expiradas (solo para administradores)
    /// </summary>
    [HttpPost("clean-expired")]
    [Authorize(Roles = "SuperAdmin,Admin,Manager")]
    public async Task<ActionResult> CleanExpiredNotifications()
    {
        try
        {
            var count = await _notificationService.CleanExpiredNotificationsAsync();

            return Ok(
                new { message = $"Se limpiaron {count} notificaciones expiradas", count = count }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning expired notifications");
            return StatusCode(500, "Error interno del servidor");
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
