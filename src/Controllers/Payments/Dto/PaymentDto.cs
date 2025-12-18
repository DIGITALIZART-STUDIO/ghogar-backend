using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;

namespace GestionHogar.Controllers.Dtos;

public class PaymentDto
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public DateTime DueDate { get; set; }
    public decimal AmountDue { get; set; }
    public bool Paid { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}
