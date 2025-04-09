using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;

namespace GestionHogar.Controllers.Dtos;

public class ClientUpdateDto
{
    public string? Name { get; set; }

    public string? CoOwner { get; set; }

    [StringLength(8)]
    public string? Dni { get; set; }

    [StringLength(11)]
    public string? Ruc { get; set; }

    public string? CompanyName { get; set; }

    public string? PhoneNumber { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    public string? Address { get; set; }

    public ClientType? Type { get; set; } // Tipo nullable
}
