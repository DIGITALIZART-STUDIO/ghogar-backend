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
        // Obtener todas las cuotas de la reserva
        var payments = await context
            .Payments.Where(p => p.ReservationId == reservationId && p.IsActive)
            .OrderBy(p => p.DueDate)
            .ToListAsync();

        // Obtener todos los pagos realizados para estas cuotas en una sola consulta
        var paymentIds = payments.Select(p => p.Id).ToList();
        var paymentDetails = await context
            .PaymentTransactionPayments.Where(ptp => paymentIds.Contains(ptp.PaymentId))
            .ToListAsync();

        // Agrupar los pagos por PaymentId para acceso rápido
        var paymentsByPaymentId = paymentDetails
            .GroupBy(ptp => ptp.PaymentId)
            .ToDictionary(g => g.Key, g => g.Sum(ptp => ptp.AmountPaid));

        var result = new List<PaymentDto>();

        foreach (var payment in payments)
        {
            // Obtener el monto pagado desde el diccionario (0 si no hay pagos)
            var totalPaidForThisPayment = paymentsByPaymentId.GetValueOrDefault(payment.Id, 0);

            // Calcular cuánto falta por pagar
            var remainingAmount = payment.AmountDue - totalPaidForThisPayment;

            // Determinar si está completamente pagada
            var isPaid = remainingAmount <= 0;

            result.Add(
                new PaymentDto
                {
                    Id = payment.Id,
                    ReservationId = payment.ReservationId,
                    DueDate = payment.DueDate,
                    AmountDue = payment.AmountDue,
                    AmountPaid = totalPaidForThisPayment,
                    RemainingAmount = Math.Max(0, remainingAmount), // No permitir valores negativos
                    Paid = isPaid,
                    CreatedAt = payment.CreatedAt,
                    ModifiedAt = payment.ModifiedAt,
                }
            );
        }

        return result;
    }
}
