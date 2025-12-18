using GestionHogar.Model;

namespace GestionHogar.Controllers.Dtos;

public class PaymentHistoryDto
{
    public string Id { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? BankName { get; set; }
    public string? Reference { get; set; }
    public PaymentStatus Status { get; set; }
    public string? Notes { get; set; }
}

public class AddPaymentHistoryDto
{
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? BankName { get; set; }
    public string? Reference { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.PENDING;
    public string? Notes { get; set; }
}

public class UpdatePaymentHistoryDto
{
    public string Id { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? BankName { get; set; }
    public string? Reference { get; set; }
    public PaymentStatus Status { get; set; }
    public string? Notes { get; set; }
}
