using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;

namespace GestionHogar.Services;

public interface IContractService
{
    Task<PaginatedResponseV2<ContractDto>> GetPaginatedAsync(
        int page,
        int pageSize,
        PaginationService paginationService,
        string? search = null,
        ContractStatus[]? status = null,
        ContractOrigin[]? origin = null,
        Guid? clientId = null,
        Guid? projectId = null,
        bool? overdue = null,
        bool? hasBalance = null,
        string? orderBy = null
    );

    Task<ContractDto?> GetByIdAsync(Guid id);

    Task<IEnumerable<PaymentDto>> GetScheduleByIdAsync(Guid id);

    Task<IEnumerable<ContractDto>> GetByClientIdAsync(Guid clientId);

    Task<ClientFinancialSummaryDto> GetFinancialSummaryByClientIdAsync(Guid clientId);

    Task<string> GenerateCodeAsync(int? year = null);

    Task<ContractDto> CreateFromPaidReservationAsync(Guid reservationId);

    Task<ContractDto> CreateAsync(CreateContractDto dto);

    Task<ContractDto> UpdateAsync(Guid contractId, UpdateContractDto dto);

    Task<ContractDto> RescindAsync(Guid contractId);

    Task RescindIfLeavingPaidReservationAsync(Guid reservationId);

    Task<ContractDto> ToggleValidationStatusAsync(Guid contractId);

    Task<byte[]> GenerateContractPdfAsync(Guid contractId);

    Task<byte[]> GenerateContractDocxAsync(Guid contractId);

    Task<byte[]> GenerateSchedulePdfAsync(Guid contractId);
}
