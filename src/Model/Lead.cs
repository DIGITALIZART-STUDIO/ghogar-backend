using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace GestionHogar.Model;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LeadStatus
{
    Registered, // Recién registrado en el sistema
    Attended, // Ya ha sido atendido por un asesor
}

public class Lead : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

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
    public required string Procedency { get; set; } // Origen del lead (Facebook, página web, Instagram, etc.)

    // Propiedades de IEntity
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}
