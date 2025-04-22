using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Model;

[Index(nameof(Dni), IsUnique = true, Name = "IX_Client_Dni")]
[Index(nameof(Ruc), IsUnique = true, Name = "IX_Client_Ruc")]
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
    public (bool isValid, string errorMessage) ValidateClientDetails()
    {
        // Validaciones comunes
        if (string.IsNullOrEmpty(Name))
            return (false, "El nombre es obligatorio");
        if (string.IsNullOrEmpty(PhoneNumber))
            return (false, "El número de teléfono es obligatorio");
        if (string.IsNullOrEmpty(Email))
            return (false, "El email es obligatorio");
        if (string.IsNullOrEmpty(Address))
            return (false, "La dirección es obligatoria");

        // Validaciones específicas por tipo
        if (Type == ClientType.Natural)
        {
            if (string.IsNullOrEmpty(Dni))
                return (false, "El DNI es obligatorio para clientes tipo Natural");
            if (Dni.Length != 8)
                return (false, "El DNI debe tener 8 caracteres");
            // Limpiar campos que no aplican
            Ruc = null;
            CompanyName = null;
        }
        else // ClientType.Juridico
        {
            if (string.IsNullOrEmpty(Ruc))
                return (false, "El RUC es obligatorio para clientes tipo Jurídico");
            if (Ruc.Length != 11)
                return (false, "El RUC debe tener 11 caracteres");

            // Si no hay CompanyName, usar el Name como CompanyName
            if (string.IsNullOrEmpty(CompanyName))
                CompanyName = Name;

            // Limpiar campos que no aplican
            Dni = null;
        }

        return (true, string.Empty);
    }

    // Mantener el método original por compatibilidad
    public bool ValidateClientData()
    {
        var (isValid, _) = ValidateClientDetails();
        return isValid;
    }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClientType
{
    Natural, // Persona natural (usará DNI)
    Juridico, // Persona jurídica/empresa (usará RUC)
}
