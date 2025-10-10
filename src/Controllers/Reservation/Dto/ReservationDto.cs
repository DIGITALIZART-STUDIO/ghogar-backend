using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;

namespace GestionHogar.Controllers.Dtos;

public class ReservationDto
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public Guid QuotationId { get; set; }
    public string QuotationCode { get; set; } = string.Empty;
    public DateOnly ReservationDate { get; set; }
    public decimal AmountPaid { get; set; }
    public Currency Currency { get; set; }
    public ReservationStatus Status { get; set; }
    public ContractValidationStatus ContractValidationStatus { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? BankName { get; set; }
    public decimal ExchangeRate { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool Notified { get; set; }
    public string? Schedule { get; set; }
    public string? CoOwners { get; set; } // JSON con los copropietarios
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}

public class ReservationWithPaymentsDto : ReservationDto
{
    public int PaymentCount { get; set; }
    public DateTime? NextPaymentDueDate { get; set; }
}
