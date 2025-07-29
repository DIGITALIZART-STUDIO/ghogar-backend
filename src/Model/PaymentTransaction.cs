using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GestionHogar.Model;

public class PaymentTransaction : BaseModel
{
    [Required]
    public DateTimeOffset PaymentDate { get; set; }

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

    public static void SetUp<A>(ModelBuilder modelBuilder)
    {
        var dateTimeOffsetToUtcConverter = new ValueConverter<DateTimeOffset, DateTimeOffset>(
            v => v.ToUniversalTime(),
            v => new DateTimeOffset(v.DateTime, TimeSpan.Zero)
        );

        modelBuilder
            .Entity<PaymentTransaction>()
            .Property(p => p.PaymentDate)
            .HasConversion(dateTimeOffsetToUtcConverter);
    }
}
