using GestionHogar.Model;

namespace GestionHogar.Controllers.Notifications.Dto;

public class NotificationCreateDto
{
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Data { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
}

public class NotificationUpdateDto
{
    public bool IsRead { get; set; }
}

public class NotificationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public NotificationPriority Priority { get; set; }
    public NotificationChannel Channel { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Data { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }

    public static NotificationDto FromEntity(Notification notification)
    {
        return new NotificationDto
        {
            Id = notification.Id,
            UserId = notification.UserId,
            UserName = notification.User?.Name ?? string.Empty,
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
    }
}

public class NotificationSummaryDto
{
    public Guid Id { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public NotificationPriority Priority { get; set; }
}

public class NotificationStatsDto
{
    public int Total { get; set; }
    public int Unread { get; set; }
    public int Read { get; set; }
    public int Expired { get; set; }
    public int ByPriority { get; set; }
    public int ByType { get; set; }
}

public class NotificationBulkUpdateDto
{
    public List<Guid> NotificationIds { get; set; } = new();
    public bool MarkAsRead { get; set; } = true;
}

public class SendToMultipleRequest
{
    public List<Guid> UserIds { get; set; } = new();
    public NotificationType Type { get; set; }
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Data { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
}
