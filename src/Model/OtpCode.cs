using System.ComponentModel.DataAnnotations;

namespace GestionHogar.Model;

public class OtpCode : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Usuario que debe ingresar el OTP (supervisor/admin que aprueba)
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Usuario que solicita la acción (asesor)
    /// </summary>
    [Required]
    public Guid RequestedByUserId { get; set; }

    /// <summary>
    /// Usuario que aprobó usando el OTP (se llena al usar el código)
    /// </summary>
    public Guid? ApprovedByUserId { get; set; }

    /// <summary>
    /// Propósito del OTP (ej: "DesbloquearDescuento")
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Purpose { get; set; } = string.Empty;

    [Required]
    [StringLength(6, MinimumLength = 6)]
    public required string Code { get; set; }

    [Required]
    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; } = false;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User? User { get; set; }
    public User? RequestedByUser { get; set; }
    public User? ApprovedByUser { get; set; }

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
    /// Marca el código como usado y registra quién lo aprobó
    /// </summary>
    public void MarkAsUsed(Guid approvedByUserId)
    {
        IsUsed = true;
        IsActive = false;
        ApprovedByUserId = approvedByUserId;
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
