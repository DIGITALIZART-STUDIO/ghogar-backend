using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;

namespace GestionHogar.Controllers.Dtos;

public class PaymentDto
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public DateTime DueDate { get; set; }
    public decimal AmountDue { get; set; }

    /// <summary>
    /// Monto que ya se ha pagado por esta cuota
    /// </summary>
    public decimal AmountPaid { get; set; }

    /// <summary>
    /// Monto que falta por pagar (AmountDue - AmountPaid)
    /// </summary>
    public decimal RemainingAmount { get; set; }

    public bool Paid { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}
