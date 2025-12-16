using System.ComponentModel.DataAnnotations;

namespace GestionHogar.Controllers.Dtos;

public class ContactCreateDto
{
    [Required]
    public required string Nombres { get; set; }

    [Required]
    public required string Apellidos { get; set; }

    [Required]
    public required string NumeroDocumento { get; set; }

    [Required]
    public required string Telefono { get; set; }

    [Required]
    [EmailAddress]
    public required string Email { get; set; }

    [Required]
    public required Guid ProjectId { get; set; }
}
