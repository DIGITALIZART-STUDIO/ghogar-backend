using GestionHogar.Dtos;
using GestionHogar.Model;

namespace GestionHogar.Services;

public interface IQuotationService
{
    Task<IEnumerable<QuotationDTO>> GetAllQuotationsAsync();
    Task<QuotationDTO?> GetQuotationByIdAsync(Guid id);
    Task<QuotationDTO?> GetQuotationByReservationIdAsync(Guid reservationId);
    Task<IEnumerable<QuotationDTO>> GetQuotationsByLeadIdAsync(Guid leadId);
    Task<IEnumerable<QuotationSummaryDTO>> GetQuotationsByAdvisorIdAsync(Guid advisorId);
    Task<PaginatedResponseV2<QuotationSummaryDTO>> GetQuotationsByAdvisorIdPaginatedAsync(
        Guid advisorId,
        int page,
        int pageSize,
        PaginationService paginationService
    );
    Task<IEnumerable<QuotationSummaryDTO>> GetAcceptedQuotationsByAdvisorIdAsync(Guid advisorId);
    Task<QuotationDTO> CreateQuotationAsync(QuotationCreateDTO dto);
    Task<QuotationDTO?> UpdateQuotationAsync(Guid id, QuotationUpdateDTO dto);
    Task<bool> DeleteQuotationAsync(Guid id);
    Task<QuotationDTO?> ChangeStatusAsync(Guid id, string status);
    Task<string> GenerateQuotationCodeAsync();
    Task<byte[]> GenerateQuotationPdfAsync(Guid quotationId);
}
