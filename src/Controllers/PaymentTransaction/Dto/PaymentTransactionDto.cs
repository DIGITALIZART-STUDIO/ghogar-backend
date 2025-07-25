using GestionHogar.Model;

namespace GestionHogar.Dtos;

public class PaymentTransactionDTO
{
    public Guid Id { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal AmountPaid { get; set; }
    public Guid? ReservationId { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? ReferenceNumber { get; set; }
    public List<PaymentDTO> Payments { get; set; } = new();
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
            Payments = transaction
                .Payments.Select(p => new PaymentDTO
                {
                    Id = p.Id,
                    AmountDue = p.AmountDue,
                    DueDate = p.DueDate,
                    Paid = p.Paid,
                })
                .ToList(),
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
