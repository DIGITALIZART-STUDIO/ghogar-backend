using GestionHogar.Model;

namespace GestionHogar.Dtos;

public class PaymentTransactionDTO
{
    public Guid Id { get; set; }
    public DateTimeOffset PaymentDate { get; set; }
    public decimal AmountPaid { get; set; }
    public Guid? ReservationId { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? ComprobanteUrl { get; set; }

    // Mantener para compatibilidad hacia atrás
    public List<PaymentDTO> Payments { get; set; } = new();

    // NUEVA: Información detallada sobre la distribución de pagos
    public List<PaymentDetailDTO> PaymentDetails { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }

    public static PaymentTransactionDTO FromEntity(PaymentTransaction transaction)
    {
        return new PaymentTransactionDTO
        {
            Id = transaction.Id,
            PaymentDate = transaction.PaymentDate,
            AmountPaid = transaction.AmountPaid,
            ReservationId = transaction.ReservationId,
            PaymentMethod = transaction.PaymentMethod,
            ReferenceNumber = transaction.ReferenceNumber,
            ComprobanteUrl = transaction.ComprobanteUrl,

            // Mantener para compatibilidad hacia atrás
            Payments = transaction
                .Payments.Select(p => new PaymentDTO
                {
                    Id = p.Id,
                    AmountDue = p.AmountDue,
                    DueDate = p.DueDate,
                    Paid = p.Paid,
                })
                .ToList(),

            // NUEVA: Información detallada sobre montos pagados por cuota
            PaymentDetails =
                transaction
                    .PaymentDetails?.Select(pd => new PaymentDetailDTO
                    {
                        PaymentId = pd.PaymentId,
                        AmountPaid = pd.AmountPaid,
                        Payment = new PaymentDTO
                        {
                            Id = pd.Payment.Id,
                            AmountDue = pd.Payment.AmountDue,
                            DueDate = pd.Payment.DueDate,
                            Paid = pd.Payment.Paid,
                        },
                    })
                    .ToList() ?? new List<PaymentDetailDTO>(),

            CreatedAt = transaction.CreatedAt,
            ModifiedAt = transaction.ModifiedAt,
        };
    }
}

public class PaymentDTO
{
    public Guid Id { get; set; }
    public decimal AmountDue { get; set; }
    public DateTime DueDate { get; set; }
    public bool Paid { get; set; }
}

/// <summary>
/// DTO que muestra información detallada sobre cuánto se pagó por cada cuota específica
/// en una transacción de pago.
/// </summary>
public class PaymentDetailDTO
{
    public Guid PaymentId { get; set; }
    public decimal AmountPaid { get; set; }
    public PaymentDTO Payment { get; set; } = new();
}

public class PaymentQuotaSimpleDTO
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string QuotationCode { get; set; } = string.Empty;
    public decimal AmountDue { get; set; }
    public DateTime DueDate { get; set; }
    public bool Paid { get; set; }
    public Currency Currency { get; set; }
}

/// <summary>
/// DTO que proporciona información completa sobre el estado de cuotas de una reserva,
/// incluyendo mínimo y máximo de cuotas a pagar.
/// </summary>
public class PaymentQuotaStatusDTO
{
    /// <summary>
    /// Lista de cuotas pendientes con montos específicos que faltan por pagar
    /// </summary>
    public List<PaymentQuotaSimpleDTO> PendingQuotas { get; set; } = new();

    /// <summary>
    /// Número mínimo de cuotas que se pueden pagar.
    /// Representa cuotas completamente impagas (0 pagos).
    /// </summary>
    public int MinQuotasToPay { get; set; }

    /// <summary>
    /// Número máximo de cuotas que se pueden pagar.
    /// Representa todas las cuotas que tienen algún monto pendiente.
    /// </summary>
    public int MaxQuotasToPay { get; set; }

    /// <summary>
    /// Monto total que falta por pagar en todas las cuotas pendientes
    /// </summary>
    public decimal TotalAmountRemaining { get; set; }

    /// <summary>
    /// Moneda de la reserva
    /// </summary>
    public Currency Currency { get; set; }
}
