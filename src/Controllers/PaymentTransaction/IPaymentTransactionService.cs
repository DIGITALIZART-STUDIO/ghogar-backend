using GestionHogar.Dtos;

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
    Task<PaymentTransactionDTO> CreateAsync(PaymentTransactionCreateDTO dto);
    Task<PaymentTransactionDTO?> UpdateAsync(Guid id, PaymentTransactionUpdateDTO dto);
    Task<bool> DeleteAsync(Guid id);
}
