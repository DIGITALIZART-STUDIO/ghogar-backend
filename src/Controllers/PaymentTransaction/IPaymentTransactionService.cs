using GestionHogar.Dtos;
using Microsoft.AspNetCore.Http;

namespace GestionHogar.Services;

public interface IPaymentTransactionService
{
    Task<IEnumerable<PaymentTransactionDTO>> GetAllAsync();
    Task<PaymentTransactionDTO?> GetByIdAsync(Guid id);
    Task<IEnumerable<PaymentTransactionDTO>> GetByReservationIdAsync(Guid reservationId);

    /// <summary>
    /// Obtiene el estado de cuotas con información de mínimo y máximo a pagar.
    /// </summary>
    /// <param name="reservationId">ID de la reserva</param>
    /// <param name="excludeTransactionId">ID de transacción a excluir (opcional)</param>
    /// <returns>DTO con cuotas pendientes, mínimo y máximo a pagar</returns>
    Task<PaymentQuotaStatusDTO> GetQuotaStatusByReservationAsync(
        Guid reservationId,
        Guid? excludeTransactionId = null
    );

    /// <summary>
    /// Crea una nueva transacción de pago.
    /// Si no se proporcionan PaymentIds, el sistema asignará automáticamente las cuotas
    /// desde la última hacia atrás (fecha más lejana a más temprana).
    /// </summary>
    Task<PaymentTransactionDTO> CreateAsync(
        PaymentTransactionCreateDTO dto,
        IFormFile? comprobanteImage = null
    );

    /// <summary>
    /// Actualiza una transacción de pago existente.
    /// Si no se proporcionan PaymentIds, el sistema asignará automáticamente las cuotas
    /// desde la última hacia atrás (fecha más lejana a más temprana).
    /// </summary>
    Task<PaymentTransactionDTO?> UpdateAsync(
        Guid id,
        PaymentTransactionUpdateDTO dto,
        IFormFile? comprobanteImage = null
    );

    Task<bool> DeleteAsync(Guid id);
}
