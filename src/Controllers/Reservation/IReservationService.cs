using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;

namespace GestionHogar.Services;

public interface IReservationService
{
    Task<IEnumerable<ReservationDto>> GetAllReservationsAsync();
    Task<PaginatedResponseV2<ReservationDto>> GetAllReservationsPaginatedAsync(
        int page,
        int pageSize,
        PaginationService paginationService,
        string? search = null,
        ReservationStatus[]? status = null,
        PaymentMethod[]? paymentMethod = null,
        Guid? projectId = null,
        string? orderBy = null
    );
    Task<PaginatedResponseV2<ReservationDto>> GetReservationsByAdvisorIdPaginatedAsync(
        Guid advisorId,
        int page,
        int pageSize,
        PaginationService paginationService,
        string? search = null,
        ReservationStatus[]? status = null,
        PaymentMethod[]? paymentMethod = null,
        Guid? projectId = null,
        string? orderBy = null
    );
    Task<IEnumerable<ReservationWithPaymentsDto>> GetAllCanceledReservationsAsync();
    Task<PaginatedResponseV2<ReservationWithPaymentsDto>> GetAllCanceledReservationsPaginatedAsync(
        int page,
        int pageSize,
        Guid? projectId = null
    );
    Task<ReservationDto?> GetReservationByIdAsync(Guid id);
    Task<Reservation> CreateReservationAsync(ReservationCreateDto reservationDto);
    Task<ReservationDto?> UpdateReservationAsync(Guid id, ReservationUpdateDto reservationDto);
    Task<bool> DeleteReservationAsync(Guid id);
    Task<IEnumerable<ReservationDto>> GetReservationsByClientIdAsync(Guid clientId);
    Task<IEnumerable<ReservationDto>> GetReservationsByQuotationIdAsync(Guid quotationId);

    Task<
        PaginatedResponseV2<ReservationPendingValidationDto>
    > GetAllCanceledPendingValidationReservationsPaginatedAsync(
        int page,
        int pageSize,
        PaginationService paginationService,
        string? search = null,
        ReservationStatus[]? status = null,
        PaymentMethod[]? paymentMethod = null,
        ContractValidationStatus[]? contractValidationStatus = null,
        Guid? projectId = null,
        string? orderBy = null
    );
    Task<ReservationDto?> ChangeStatusAsync(Guid id, ReservationStatusDto statusDto);
    Task<byte[]> GenerateReservationPdfAsync(Guid reservationId);
    Task<byte[]> GenerateSchedulePdfAsync(Guid reservationId);
    Task<byte[]> GenerateProcessedPaymentsPdfAsync(Guid reservationId);
    Task<byte[]> GenerateReceiptPdfAsync(Guid reservationId);
    Task<byte[]> GenerateContractPdfAsync(Guid reservationId);
    Task<byte[]> GenerateContractDocxAsync(Guid reservationId);
    Task<bool> ToggleContractValidationStatusAsync(Guid reservationId);

    // Payment History Management
    Task<List<PaymentHistoryDto>> GetPaymentHistoryAsync(Guid reservationId);
    Task<PaymentHistoryDto> AddPaymentToHistoryAsync(
        Guid reservationId,
        AddPaymentHistoryDto paymentDto
    );
    Task<PaymentHistoryDto> UpdatePaymentInHistoryAsync(
        Guid reservationId,
        UpdatePaymentHistoryDto paymentDto
    );
    Task<bool> RemovePaymentFromHistoryAsync(Guid reservationId, string paymentId);

    /// <summary>
    /// Obtiene todas las reservas con cuotas pendientes y paginación
    /// </summary>
    Task<
        PaginatedResponseV2<ReservationWithPendingPaymentsDto>
    > GetAllReservationsWithPendingPaymentsPaginatedAsync(
        int page,
        int pageSize,
        PaginationService paginationService,
        Guid currentUserId,
        List<string> currentUserRoles,
        string? search = null,
        ReservationStatus[]? status = null,
        PaymentMethod[]? paymentMethod = null,
        ContractValidationStatus[]? contractValidationStatus = null,
        Guid? projectId = null,
        string? orderBy = null
    );
}
