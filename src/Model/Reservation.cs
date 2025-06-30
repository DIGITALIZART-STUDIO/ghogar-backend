using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace GestionHogar.Model;

public class Reservation : BaseModel
{
    // Relación con el cliente que reserva
    [Required]
    public Guid ClientId { get; set; }

    [ForeignKey("ClientId")]
    public required Client Client { get; set; }

    // Relación con la cotización
    [Required]
    public Guid QuotationId { get; set; }

    [ForeignKey("QuotationId")]
    public required Quotation Quotation { get; set; }

    // Fecha en que se hizo la reserva
    [Required]
    public DateOnly ReservationDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    // Monto pagado para la separación
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountPaid { get; set; }

    // Moneda (soles o dólares)
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Currency Currency { get; set; } = Currency.SOLES;

    // Estado de la reserva
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ReservationStatus Status { get; set; } = ReservationStatus.ISSUED;

    // Método de pago
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PaymentMethod PaymentMethod { get; set; }

    // Nombre del banco si es depósito/transferencia (opcional)
    public string? BankName { get; set; }

    // Tipo de cambio del día (editable)
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal ExchangeRate { get; set; }

    // Fecha de vencimiento de la reserva (4 días después)
    [Required]
    public DateTime ExpiresAt { get; set; }

    // Si el cliente ha sido notificado
    public bool Notified { get; set; } = false;

    // Cronograma de pagos (número de meses, fecha inicio de pago, monto que se tienen que dividir)
    public string? Schedule { get; set; }

    // Navegación hacia los pagos programados
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Currency
{
    SOLES,
    DOLARES,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReservationStatus
{
    ISSUED,
    CANCELED,
    ANULATED,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentMethod
{
    CASH, // Efectivo
    BANK_DEPOSIT, // Depósito bancario
    BANK_TRANSFER, // Transferencia bancaria
}
