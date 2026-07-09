using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;

namespace GestionHogar.Controllers.Dtos;

public class LeadCreateFromPhoneDto
{
    [Required]
    public required string PhoneNumber { get; set; }

    [Required]
    public LeadCaptureSource CaptureSource { get; set; }

    public Guid? AssignedToId { get; set; }

    public Guid? ProjectId { get; set; }
}
