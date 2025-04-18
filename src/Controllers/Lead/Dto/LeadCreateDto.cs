using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;

namespace GestionHogar.Controllers.Dtos;

public class LeadCreateDto
{
    public Guid? ClientId { get; set; }
    public Guid? AssignedToId { get; set; }

    [Required]
    public LeadStatus Status { get; set; } = LeadStatus.Registered;

    [Required]
    public required string Procedency { get; set; }
}
