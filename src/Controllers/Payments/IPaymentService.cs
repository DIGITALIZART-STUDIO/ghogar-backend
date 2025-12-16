using GestionHogar.Controllers.Dtos;

namespace GestionHogar.Services;

public interface IPaymentService
{
    /// <summary>
    /// Obtiene el cronograma de pagos de una reserva con información detallada
    /// sobre montos pagados y pendientes por cada cuota.
    /// </summary>
    /// <param name="reservationId">ID de la reserva</param>
    /// <returns>Lista de cuotas con montos pagados y pendientes</returns>
    Task<IEnumerable<PaymentDto>> GetPaymentScheduleByReservationIdAsync(Guid reservationId);
}
