using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace GestionHogar.Model;

public class PaymentTransaction : BaseModel
{
    [Required]
    public DateTime PaymentDate { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountPaid { get; set; }

    // Relación con la reserva (opcional)
    public Guid? ReservationId { get; set; }

    [ForeignKey("ReservationId")]
    public Reservation? Reservation { get; set; }

    // Relación con las cuotas (pagos programados) que cubre esta transacción
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    // Forma de pago
    [Required]
    public PaymentMethod PaymentMethod { get; set; }

    // Número de operación, referencia, voucher, etc.
    public string? ReferenceNumber { get; set; }
}
