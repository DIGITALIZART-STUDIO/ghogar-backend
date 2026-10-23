using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionHogar.Model;

public class Payment : BaseModel
{
    // Relación con la reserva (auditoría / transición; el dueño es el contrato)
    public Guid? ReservationId { get; set; }

    [ForeignKey("ReservationId")]
    public Reservation? Reservation { get; set; }

    public Guid? ContractId { get; set; }

    [ForeignKey("ContractId")]
    public Contract? Contract { get; set; }

    public PaymentType Type { get; set; } = PaymentType.Installment;

    public int Sequence { get; set; }

    // Fecha de vencimiento de la cuota
    [Required]
    public DateTime DueDate { get; set; }

    // Monto a pagar para esta cuota
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountDue { get; set; }

    // Estado del pago
    public bool Paid { get; set; } = false;
}
