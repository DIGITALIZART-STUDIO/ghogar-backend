using System.ComponentModel.DataAnnotations;

namespace GestionHogar.Controllers.Dtos;

public class ReferralCreateDto
{
    // Datos del referidor (quien hace la referencia)
    [Required]
    public required ReferrerDataDto Referrer { get; set; }

    // Datos del referenciado (quien es referido)
    [Required]
    public required ReferredDataDto Referred { get; set; }
}

public class ReferrerDataDto
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
}

public class ReferredDataDto
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
}
