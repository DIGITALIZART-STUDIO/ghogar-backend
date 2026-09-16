using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;
using GestionHogar.Utils;

namespace GestionHogar.Controllers.Dtos;

public class ClientCreateDto
{
    [Required]
    public required string Name { get; set; }

    public string? CoOwners { get; set; } // JSON con los copropietarios

    // Opcional. Si se envía, debe tener 8 caracteres
    [StringLength(8)]
    public string? Dni { get; set; }

    // Opcional. Si se envía, debe tener 11 caracteres
    [StringLength(11)]
    public string? Ruc { get; set; }

    public string? CompanyName { get; set; }

    [Required]
    public required string PhoneNumber { get; set; }

    [OptionalEmailAddress]
    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? Country { get; set; }

    [Required]
    public ClientType Type { get; set; }

    public bool SeparateProperty { get; set; } = false;

    public string? SeparatePropertyData { get; set; } // JSON con datos de separación de bienes
}
