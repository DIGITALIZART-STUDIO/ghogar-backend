using GestionHogar.Dtos;
using Microsoft.AspNetCore.Http;

namespace GestionHogar.Services;

public interface IPaymentTransactionService
{
    Task<IEnumerable<PaymentTransactionDTO>> GetAllAsync();
    Task<PaymentTransactionDTO?> GetByIdAsync(Guid id);
    Task<IEnumerable<PaymentTransactionDTO>> GetByReservationIdAsync(Guid reservationId);

    Task<IEnumerable<PaymentQuotaSimpleDTO>> GetQuotaStatusByReservationAsync(
        Guid reservationId,
        Guid? excludeTransactionId = null
    );
    Task<PaymentTransactionDTO> CreateAsync(
        PaymentTransactionCreateDTO dto,
        IFormFile? comprobanteImage = null
    );
    Task<PaymentTransactionDTO?> UpdateAsync(
        Guid id,
        PaymentTransactionUpdateDTO dto,
        IFormFile? comprobanteImage = null
    );
    Task<bool> DeleteAsync(Guid id);
}
