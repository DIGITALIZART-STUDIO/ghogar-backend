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

    [Required]
    public List<Guid> PaymentIds { get; set; } = new();
}
