using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace GestionHogar.Model;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LeadStatus
{
    Registered, // Recién registrado en el sistema
    Attended, // Ya ha sido atendido por un asesor
    InFollowUp, // En seguimiento
    Completed, // Terminado
    Canceled, // Cancelado
    Expired, // Expirado (después de 7 días sin actividad)
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LeadCaptureSource
{
    Company, // Empresa
    PersonalFacebook, // FB personal
    RealEstateFair, // Feria inmobiliaria
    Institutional, // Institucional
    Loyalty, // Fidelizado
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LeadCompletionReason
{
    NotInterested, // Cliente no interesado
    InFollowUp, // En seguimiento
    Sale, // Venta
}

public class Lead : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Código único de cotización
    [Required]
    public required string Code { get; set; }

    // Relación con el cliente
    public Guid? ClientId { get; set; }

    [ForeignKey("ClientId")]
    public Client? Client { get; set; }

    // Asesor de ventas asignado
    public Guid? AssignedToId { get; set; }

    [ForeignKey("AssignedToId")]
    public User? AssignedTo { get; set; }

    [Required]
    public LeadStatus Status { get; set; } = LeadStatus.Registered;

    [Required]
    public LeadCaptureSource CaptureSource { get; set; } // Medio de captación

    // Fechas de ingreso y vencimiento (período de 7 días)
    [Required]
    public DateTime EntryDate { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime ExpirationDate { get; set; } = DateTime.UtcNow.AddDays(7);

    // Historial de reciclajes
    public int RecycleCount { get; set; } = 0;

    // Fecha del último reciclaje
    public DateTime? LastRecycledAt { get; set; }

    // Usuario que realizó el último reciclaje
    public Guid? LastRecycledById { get; set; }

    [ForeignKey("LastRecycledById")]
    public User? LastRecycledBy { get; set; }

    // Relación con proyecto
    public Guid? ProjectId { get; set; }

    [ForeignKey("ProjectId")]
    public Project? Project { get; set; }

    // Razón de finalización del lead
    public LeadCompletionReason? CompletionReason { get; set; }

    // Motivo de cancelación (si aplica)
    public string? CancellationReason { get; set; }

    // Propiedades de IEntity
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    // Método para reciclar un lead expirado
    public void RecycleLead(Guid userId)
    {
        if (Status == LeadStatus.Expired || Status == LeadStatus.Canceled)
        {
            Status = LeadStatus.InFollowUp;
            ExpirationDate = DateTime.UtcNow.AddDays(7);
            RecycleCount++;
            LastRecycledAt = DateTime.UtcNow;
            LastRecycledById = userId;
            ModifiedAt = DateTime.UtcNow;
        }
    }
}
