using System.ComponentModel.DataAnnotations;

namespace GestionHogar.Model;

public class OtpCode : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [StringLength(6, MinimumLength = 6)]
    public required string Code { get; set; }

    [Required]
    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; } = false;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public User? User { get; set; }

    /// <summary>
    /// Genera un código OTP de 6 dígitos
    /// </summary>
    public static string GenerateOtpCode()
    {
        var random = new Random();
        return random.Next(100000, 999999).ToString();
    }

    /// <summary>
    /// Verifica si el código OTP es válido (no expirado y no usado)
    /// </summary>
    public bool IsValid()
    {
        return IsActive && !IsUsed && DateTime.UtcNow <= ExpiresAt;
    }

    /// <summary>
    /// Marca el código como usado
    /// </summary>
    public void MarkAsUsed()
    {
        IsUsed = true;
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Invalida el código (lo marca como inactivo)
    /// </summary>
    public void Invalidate()
    {
        IsActive = false;
        ModifiedAt = DateTime.UtcNow;
    }
}
