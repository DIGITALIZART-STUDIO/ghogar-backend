using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Services;

public class PaymentService(DatabaseContext context) : IPaymentService
{
    public async Task<IEnumerable<PaymentDto>> GetPaymentScheduleByReservationIdAsync(
        Guid reservationId
    )
    {
        return await context
            .Payments.Where(p => p.ReservationId == reservationId && p.IsActive)
            .OrderBy(p => p.DueDate)
            .Select(p => new PaymentDto
            {
                Id = p.Id,
                ReservationId = p.ReservationId,
                DueDate = p.DueDate,
                AmountDue = p.AmountDue,
                Paid = p.Paid,
                CreatedAt = p.CreatedAt,
                ModifiedAt = p.ModifiedAt,
            })
            .ToListAsync();
    }
}
