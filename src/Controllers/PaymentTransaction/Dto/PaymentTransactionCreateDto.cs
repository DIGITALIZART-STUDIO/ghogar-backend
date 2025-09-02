using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;

namespace GestionHogar.Dtos;

public class PaymentTransactionCreateDTO
{
    [Required]
    public DateTime PaymentDate { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0")]
    public decimal AmountPaid { get; set; }

    public Guid? ReservationId { get; set; }

    [Required]
    public PaymentMethod PaymentMethod { get; set; }

    [StringLength(100)]
    public string? ReferenceNumber { get; set; }

    [StringLength(500)]
    public string? ComprobanteUrl { get; set; }

    /// <summary>
    /// Lista de IDs de cuotas específicas a pagar.
    /// Si no se proporciona, el sistema asignará automáticamente las cuotas.
    /// </summary>
    public List<Guid>? PaymentIds { get; set; }

    /// <summary>
    /// Estrategia de asignación automática cuando no se proporcionan PaymentIds.
    /// - true: Empezar desde la última cuota (fecha más lejana) - MAX
    /// - false: Empezar desde la primera cuota (fecha más temprana) - MIN
    /// Por defecto es true (MAX) para mantener compatibilidad.
    /// </summary>
    public bool StartFromLastCuota { get; set; } = true;
}
