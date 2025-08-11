using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;

namespace GestionHogar.Services;

public interface IReservationService
{
    Task<IEnumerable<ReservationDto>> GetAllReservationsAsync();
    Task<IEnumerable<ReservationWithPaymentsDto>> GetAllCanceledReservationsAsync();
    Task<PaginatedResponseV2<ReservationWithPaymentsDto>> GetAllCanceledReservationsPaginatedAsync(
        int page,
        int pageSize
    );
    Task<ReservationDto?> GetReservationByIdAsync(Guid id);
    Task<Reservation> CreateReservationAsync(ReservationCreateDto reservationDto);
    Task<ReservationDto?> UpdateReservationAsync(Guid id, ReservationUpdateDto reservationDto);
    Task<bool> DeleteReservationAsync(Guid id);
    Task<IEnumerable<ReservationDto>> GetReservationsByClientIdAsync(Guid clientId);
    Task<IEnumerable<ReservationDto>> GetReservationsByQuotationIdAsync(Guid quotationId);

    Task<
        PaginatedResponseV2<ReservationDto>
    > GetAllCanceledPendingValidationReservationsPaginatedAsync(
        int page,
        int pageSize,
        PaginationService paginationService
    );
    Task<ReservationDto?> ChangeStatusAsync(Guid id, string status);
    Task<byte[]> GenerateReservationPdfAsync(Guid reservationId);
    Task<byte[]> GenerateSchedulePdfAsync(Guid reservationId);
    Task<byte[]> GenerateProcessedPaymentsPdfAsync(Guid reservationId);
    Task<byte[]> GenerateReceiptPdfAsync(Guid reservationId);
    Task<byte[]> GenerateContractPdfAsync(Guid reservationId);
    Task<byte[]> GenerateContractDocxAsync(Guid reservationId);
    Task<bool> ToggleContractValidationStatusAsync(Guid reservationId);
}
