using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionHogar.Model;

public class Client : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public required string Name { get; set; } // Nombre de persona natural o razón social

    public string? CoOwner { get; set; } // Opcional - Nombre del copropietario

    [StringLength(8)]
    public string? Dni { get; set; } // Para personas naturales

    [StringLength(11)]
    public string? Ruc { get; set; } // Para empresas, opcional

    public string? CompanyName { get; set; } // Nombre comercial (si es diferente de la razón social)

    [Required]
    public required string PhoneNumber { get; set; }

    [Required]
    [EmailAddress]
    public required string Email { get; set; }

    [Required]
    public required string Address { get; set; } // Dirección del cliente

    [Required]
    public ClientType Type { get; set; } // Natural o Jurídico

    // IEntity implementation
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    [NotMapped] // No se guardará en la base de datos
    public string DisplayName => Type == ClientType.Natural ? Name : CompanyName ?? Name;

    // Validación para asegurar datos correctos por tipo de cliente
    public bool ValidateClientData()
    {
        if (Type == ClientType.Natural)
        {
            // Persona natural debe tener DNI
            return !string.IsNullOrEmpty(Dni) && Dni.Length == 8;
        }
        else // ClientType.Juridico
        {
            // Persona jurídica debe tener RUC
            return !string.IsNullOrEmpty(Ruc) && Ruc.Length == 11;
        }
    }
}

// Agrega este enum al final del archivo, después de cerrar la clase Client
public enum ClientType
{
    Natural, // Persona natural (usará DNI)
    Juridico, // Persona jurídica/empresa (usará RUC)
}
