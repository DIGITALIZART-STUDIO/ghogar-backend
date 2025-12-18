using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;

namespace GestionHogar.Controllers.Dtos;

public class ReservationUpdateDto
{
    public DateOnly ReservationDate { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "El monto pagado debe ser mayor a 0")]
    public decimal AmountPaid { get; set; }

    public Currency Currency { get; set; }

    public ReservationStatus Status { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public string? BankName { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "El tipo de cambio debe ser mayor a 0")]
    public decimal ExchangeRate { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool Notified { get; set; }

    public string? Schedule { get; set; }
}
