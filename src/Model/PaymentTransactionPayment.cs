using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionHogar.Model;

public class PaymentTransactionPayment
{
    [Required]
    public Guid PaymentTransactionId { get; set; }

    [Required]
    public Guid PaymentId { get; set; }

    // Monto específico pagado por esta cuota en esta transacción
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountPaid { get; set; }

    // Relaciones
    [ForeignKey("PaymentTransactionId")]
    public PaymentTransaction PaymentTransaction { get; set; } = null!;

    [ForeignKey("PaymentId")]
    public Payment Payment { get; set; } = null!;
}
