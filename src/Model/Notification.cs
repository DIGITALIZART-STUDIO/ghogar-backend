using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace GestionHogar.Model;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationType
{
    LeadAssigned, // Lead asignado a asesor
    LeadExpired, // Lead expirado
    LeadCompleted, // Lead completado
    PaymentReceived, // Pago recibido
    QuotationCreated, // Cotización creada
    ReservationCreated, // Reserva creada
    SystemAlert, // Alerta del sistema
    Custom, // Notificación personalizada
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationPriority
{
    Low, // Baja prioridad
    Normal, // Prioridad normal
    High, // Alta prioridad
    Urgent, // Urgente
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationChannel
{
    InApp, // Solo en la aplicación
    Email, // Solo por email
    Both, // En app + email
    Push, // Push notification
}

public class Notification : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Usuario destinatario
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey("UserId")]
    public User User { get; set; } = null!;

    // Tipo de notificación
    [Required]
    public NotificationType Type { get; set; }

    // Prioridad de la notificación
    [Required]
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;

    // Canal de notificación
    [Required]
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;

    // Título de la notificación
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    // Mensaje de la notificación
    [Required]
    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    // Datos adicionales en JSON
    public string? Data { get; set; }

    // Estado de lectura
    public bool IsRead { get; set; } = false;

    // Fecha de lectura
    public DateTime? ReadAt { get; set; }

    // Fecha de envío
    public DateTime? SentAt { get; set; }

    // Fecha de expiración (opcional)
    public DateTime? ExpiresAt { get; set; }

    // Relación con entidades relacionadas (opcional)
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; } // "Lead", "Payment", "Quotation", etc.

    // Propiedades de IEntity
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    // Métodos de utilidad
    public void MarkAsRead()
    {
        IsRead = true;
        ReadAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkAsSent()
    {
        SentAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }

    public bool IsExpired()
    {
        return ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
    }

    public bool ShouldSendEmail()
    {
        return Channel == NotificationChannel.Email || Channel == NotificationChannel.Both;
    }

    public bool ShouldShowInApp()
    {
        return Channel == NotificationChannel.InApp || Channel == NotificationChannel.Both;
    }
}
