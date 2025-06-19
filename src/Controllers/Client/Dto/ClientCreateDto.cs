using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;

namespace GestionHogar.Controllers.Dtos;

public class ClientCreateDto
{
    [Required]
    public required string Name { get; set; }

    public string? CoOwners { get; set; } // JSON con los copropietarios

    // Solo requerido para Type = Natural
    [StringLength(8)]
    public string? Dni { get; set; }

    // Solo requerido para Type = Juridico
    [StringLength(11)]
    public string? Ruc { get; set; }

    public string? CompanyName { get; set; }

    [Required]
    public required string PhoneNumber { get; set; }

    [Required]
    [EmailAddress]
    public required string Email { get; set; }

    [Required]
    public required string Address { get; set; }

    public string? Country { get; set; }

    [Required]
    public ClientType Type { get; set; }

    public bool SeparateProperty { get; set; } = false;

    public string? SeparatePropertyData { get; set; } // JSON con datos de separación de bienes
}
