using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;
using GestionHogar.Utils;

namespace GestionHogar.Controllers.Dtos;

public class ClientUpdateDto
{
    public string? Name { get; set; }

    public string? CoOwners { get; set; } // JSON con los copropietarios

    [StringLength(8)]
    public string? Dni { get; set; }

    [StringLength(11)]
    public string? Ruc { get; set; }

    public string? CompanyName { get; set; }

    public string? PhoneNumber { get; set; }

    [OptionalEmailAddress]
    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? Country { get; set; }

    public ClientType? Type { get; set; } // Tipo nullable

    public bool? SeparateProperty { get; set; }

    public string? SeparatePropertyData { get; set; } // JSON con datos de separación de bienes
}
