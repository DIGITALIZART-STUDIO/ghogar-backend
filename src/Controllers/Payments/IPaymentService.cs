using GestionHogar.Controllers.Dtos;

namespace GestionHogar.Services;

public interface IPaymentService
{
    Task<IEnumerable<PaymentDto>> GetPaymentScheduleByReservationIdAsync(Guid reservationId);
}
