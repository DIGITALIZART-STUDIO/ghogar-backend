using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;

namespace GestionHogar.Controllers.Dtos;

public class ReservationCreateDto
{
    [Required]
    public Guid ClientId { get; set; }

    [Required]
    public Guid QuotationId { get; set; }

    public DateOnly ReservationDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto pagado debe ser mayor a 0")]
    public decimal AmountPaid { get; set; }

    public Currency Currency { get; set; } = Currency.SOLES;

    [Required]
    public PaymentMethod PaymentMethod { get; set; }

    public string? BankName { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "El tipo de cambio debe ser mayor a 0")]
    public decimal ExchangeRate { get; set; }

    [Required]
    public DateTime ExpiresAt { get; set; }

    public string? Schedule { get; set; }
}
