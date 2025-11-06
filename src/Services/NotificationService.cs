using GestionHogar.Controllers;
using GestionHogar.Controllers.Notifications.Dto;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GestionHogar.Services;

public interface INotificationService
{
    // Métodos de creación
    Task<Notification> CreateNotificationAsync(NotificationCreateDto dto);
    Task<Notification> CreateNotificationAsync(
        Guid userId,
        NotificationType type,
        string title,
        string message,
        NotificationPriority priority = NotificationPriority.Normal,
        NotificationChannel channel = NotificationChannel.InApp,
        string? data = null,
        DateTime? expiresAt = null,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null
    );

    // Métodos de consulta
    Task<PaginatedResponse<NotificationDto>> GetUserNotificationsAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        bool? isRead = null,
        NotificationType? type = null,
        NotificationPriority? priority = null
    );

    Task<NotificationDto?> GetNotificationByIdAsync(Guid id, Guid userId);
    Task<NotificationStatsDto> GetUserNotificationStatsAsync(Guid userId);

    // Métodos de actualización
    Task<bool> MarkAsReadAsync(Guid id, Guid userId);
    Task<bool> MarkAsUnreadAsync(Guid id, Guid userId);
    Task<int> MarkAllAsReadAsync(Guid userId);
    Task<int> MarkMultipleAsReadAsync(List<Guid> notificationIds, Guid userId);
    Task<bool> DeleteNotificationAsync(Guid id, Guid userId);

    // Métodos de envío
    Task<bool> SendNotificationAsync(Notification notification);
    Task<bool> SendNotificationByIdAsync(Guid notificationId);
    Task<int> SendPendingNotificationsAsync();

    // Métodos de limpieza
    Task<int> CleanExpiredNotificationsAsync();
    Task<int> CleanOldNotificationsAsync(int daysOld = 30);
}

public class NotificationService : INotificationService
{
    private readonly DatabaseContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        DatabaseContext context,
        IEmailService emailService,
        ILogger<NotificationService> logger
    )
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Notification> CreateNotificationAsync(NotificationCreateDto dto)
    {
        var notification = new Notification
        {
            UserId = dto.UserId,
            Type = dto.Type,
            Priority = dto.Priority,
            Channel = dto.Channel,
            Title = dto.Title,
            Message = dto.Message,
            Data = dto.Data,
            ExpiresAt = dto.ExpiresAt,
            RelatedEntityId = dto.RelatedEntityId,
            RelatedEntityType = dto.RelatedEntityType,
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "✅ [NotificationService] Notification created: {NotificationId} for user {UserId}",
            notification.Id,
            notification.UserId
        );

        // 🚀 EMISIÓN INMEDIATA: Enviar notificación en tiempo real si usuario está conectado
        try
        {
            var notificationDto = NotificationDto.FromEntity(notification);
            GestionHogar.Controllers.NotificationStreamController.EnqueueNotificationForUser(
                notification.UserId,
                notificationDto
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to queue notification {NotificationId} for instant delivery, will be delivered on next poll",
                notification.Id
            );
        }

        return notification;
    }

    public async Task<Notification> CreateNotificationAsync(
        Guid userId,
        NotificationType type,
        string title,
        string message,
        NotificationPriority priority = NotificationPriority.Normal,
        NotificationChannel channel = NotificationChannel.InApp,
        string? data = null,
        DateTime? expiresAt = null,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null
    )
    {
        var dto = new NotificationCreateDto
        {
            UserId = userId,
            Type = type,
            Priority = priority,
            Channel = channel,
            Title = title,
            Message = message,
            Data = data,
            ExpiresAt = expiresAt,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType,
        };

        return await CreateNotificationAsync(dto);
    }

    public async Task<PaginatedResponse<NotificationDto>> GetUserNotificationsAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        bool? isRead = null,
        NotificationType? type = null,
        NotificationPriority? priority = null
    )
    {
        var query = _context
            .Notifications.Where(n => n.UserId == userId && n.IsActive)
            .Include(n => n.User)
            .AsQueryable();

        // Aplicar filtros
        if (isRead.HasValue)
        {
            query = query.Where(n => n.IsRead == isRead.Value);
        }

        if (type.HasValue)
        {
            query = query.Where(n => n.Type == type.Value);
        }

        if (priority.HasValue)
        {
            query = query.Where(n => n.Priority == priority.Value);
        }

        // Ordenar por fecha de creación (más recientes primero)
        query = query.OrderByDescending(n => n.CreatedAt);

        var totalCount = await query.CountAsync();
        var notifications = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => NotificationDto.FromEntity(n))
            .ToListAsync();

        return new PaginatedResponse<NotificationDto>
        {
            Items = notifications,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
        };
    }

    public async Task<NotificationDto?> GetNotificationByIdAsync(Guid id, Guid userId)
    {
        var notification = await _context
            .Notifications.Include(n => n.User)
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId && n.IsActive);

        return notification != null ? NotificationDto.FromEntity(notification) : null;
    }

    public async Task<NotificationStatsDto> GetUserNotificationStatsAsync(Guid userId)
    {
        var stats = await _context
            .Notifications.Where(n => n.UserId == userId && n.IsActive)
            .GroupBy(n => 1)
            .Select(g => new NotificationStatsDto
            {
                Total = g.Count(),
                Unread = g.Count(n => !n.IsRead),
                Read = g.Count(n => n.IsRead),
                Expired = g.Count(n => n.ExpiresAt.HasValue && n.ExpiresAt.Value < DateTime.UtcNow),
                ByPriority = g.Count(n =>
                    n.Priority == NotificationPriority.High
                    || n.Priority == NotificationPriority.Urgent
                ),
                ByType = g.Count(n =>
                    n.Type == NotificationType.LeadAssigned
                    || n.Type == NotificationType.PaymentReceived
                ),
            })
            .FirstOrDefaultAsync();

        return stats ?? new NotificationStatsDto();
    }

    public async Task<bool> MarkAsReadAsync(Guid id, Guid userId)
    {
        var notification = await _context.Notifications.FirstOrDefaultAsync(n =>
            n.Id == id && n.UserId == userId && n.IsActive
        );

        if (notification == null)
            return false;

        notification.MarkAsRead();
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Notification {NotificationId} marked as read by user {UserId}",
            id,
            userId
        );

        return true;
    }

    public async Task<bool> MarkAsUnreadAsync(Guid id, Guid userId)
    {
        var notification = await _context.Notifications.FirstOrDefaultAsync(n =>
            n.Id == id && n.UserId == userId && n.IsActive
        );

        if (notification == null)
            return false;

        notification.IsRead = false;
        notification.ReadAt = null;
        notification.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> MarkAllAsReadAsync(Guid userId)
    {
        var notifications = await _context
            .Notifications.Where(n => n.UserId == userId && n.IsActive && !n.IsRead)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.MarkAsRead();
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Marked {Count} notifications as read for user {UserId}",
            notifications.Count,
            userId
        );

        return notifications.Count;
    }

    public async Task<int> MarkMultipleAsReadAsync(List<Guid> notificationIds, Guid userId)
    {
        var notifications = await _context
            .Notifications.Where(n =>
                notificationIds.Contains(n.Id) && n.UserId == userId && n.IsActive
            )
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.MarkAsRead();
        }

        await _context.SaveChangesAsync();
        return notifications.Count;
    }

    public async Task<bool> DeleteNotificationAsync(Guid id, Guid userId)
    {
        var notification = await _context.Notifications.FirstOrDefaultAsync(n =>
            n.Id == id && n.UserId == userId && n.IsActive
        );

        if (notification == null)
            return false;

        notification.IsActive = false;
        notification.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SendNotificationAsync(Notification notification)
    {
        try
        {
            if (notification.ShouldSendEmail())
            {
                var emailRequest = new EmailRequest
                {
                    To = notification.User.Email ?? "",
                    Subject = notification.Title,
                    Content = notification.Message,
                };

                var emailSent = await _emailService.SendEmailAsync(emailRequest);
                if (emailSent)
                {
                    notification.MarkAsSent();
                    await _context.SaveChangesAsync();
                }

                return emailSent;
            }

            // Para notificaciones in-app, solo marcamos como enviada
            notification.MarkAsSent();
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification {NotificationId}", notification.Id);
            return false;
        }
    }

    public async Task<bool> SendNotificationByIdAsync(Guid notificationId)
    {
        var notification = await _context
            .Notifications.Include(n => n.User)
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.IsActive);

        if (notification == null)
            return false;

        return await SendNotificationAsync(notification);
    }

    public async Task<int> SendPendingNotificationsAsync()
    {
        var pendingNotifications = await _context
            .Notifications.Include(n => n.User)
            .Where(n => n.IsActive && n.SentAt == null && !n.IsExpired())
            .ToListAsync();

        int sentCount = 0;
        foreach (var notification in pendingNotifications)
        {
            if (await SendNotificationAsync(notification))
            {
                sentCount++;
            }
        }

        _logger.LogInformation("Sent {Count} pending notifications", sentCount);
        return sentCount;
    }

    public async Task<int> CleanExpiredNotificationsAsync()
    {
        var expiredNotifications = await _context
            .Notifications.Where(n =>
                n.IsActive && n.ExpiresAt.HasValue && n.ExpiresAt.Value < DateTime.UtcNow
            )
            .ToListAsync();

        foreach (var notification in expiredNotifications)
        {
            notification.IsActive = false;
            notification.ModifiedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Cleaned {Count} expired notifications", expiredNotifications.Count);
        return expiredNotifications.Count;
    }

    public async Task<int> CleanOldNotificationsAsync(int daysOld = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
        var oldNotifications = await _context
            .Notifications.Where(n => n.IsActive && n.CreatedAt < cutoffDate)
            .ToListAsync();

        foreach (var notification in oldNotifications)
        {
            notification.IsActive = false;
            notification.ModifiedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Cleaned {Count} old notifications", oldNotifications.Count);
        return oldNotifications.Count;
    }
}
