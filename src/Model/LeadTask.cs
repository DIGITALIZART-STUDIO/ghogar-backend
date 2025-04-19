using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace GestionHogar.Model;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskType
{
    Call, // Llamada telefónica
    Meeting, // Reunión
    Email, // Correo electrónico
    Visit, // Visita
    Other, // Otro tipo
}

public class LeadTask : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Relación con el lead
    [Required]
    public Guid LeadId { get; set; }

    [ForeignKey("LeadId")]
    public Lead? Lead { get; set; }

    // Usuario asignado a la tarea
    [Required]
    public Guid AssignedToId { get; set; }

    [ForeignKey("AssignedToId")]
    public User? AssignedTo { get; set; }

    [Required]
    public required string Description { get; set; }

    [Required]
    public DateTime ScheduledDate { get; set; }

    public DateTime? CompletedDate { get; set; }

    public bool IsCompleted { get; set; } = false;

    public TaskType Type { get; set; } = TaskType.Other;

    // IEntity implementation
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}
