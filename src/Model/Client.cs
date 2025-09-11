using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Model;

[Index(nameof(Dni), IsUnique = true, Name = "IX_Client_Dni")]
[Index(nameof(Ruc), IsUnique = true, Name = "IX_Client_Ruc")]
[Index(nameof(PhoneNumber), IsUnique = true, Name = "IX_Client_PhoneNumber")]
public class Client : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? Name { get; set; } // Ahora opcional

    [StringLength(8)]
    public string? Dni { get; set; } // Opcional

    [StringLength(11)]
    public string? Ruc { get; set; } // Opcional

    public string? CompanyName { get; set; } // Opcional

    [Required]
    public required string PhoneNumber { get; set; } // Solo este es obligatorio

    [EmailAddress]
    public string? Email { get; set; } // Ahora opcional

    public string? Address { get; set; } // Ahora opcional

    public string? Country { get; set; } // Opcional

    public ClientType? Type { get; set; } // Ahora opcional

    [Column(TypeName = "jsonb")]
    public string? CoOwners { get; set; } // Opcional

    public bool SeparateProperty { get; set; } = false;

    [Column(TypeName = "jsonb")]
    public string? SeparatePropertyData { get; set; } // Opcional

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public string DisplayName => Type == ClientType.Natural ? Name : CompanyName ?? Name;

    // Validación mínima solo para importación
    public (bool isValid, string errorMessage) ValidateClientDetails()
    {
        if (string.IsNullOrEmpty(PhoneNumber))
            return (false, "El número de teléfono es obligatorio");
        return (true, string.Empty);
    }

    public bool ValidateClientData()
    {
        var (isValid, _) = ValidateClientDetails();
        return isValid;
    }

    // Navegación hacia los referidos que ha hecho este cliente
    public ICollection<Referral>? Referrals { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClientType
{
    Natural,
    Juridico,
}
