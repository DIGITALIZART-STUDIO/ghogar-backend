using GestionHogar.Dtos;

namespace GestionHogar.Services;

public interface IQuotationService
{
    Task<IEnumerable<QuotationDTO>> GetAllQuotationsAsync();
    Task<QuotationDTO?> GetQuotationByIdAsync(Guid id);
    Task<IEnumerable<QuotationDTO>> GetQuotationsByLeadIdAsync(Guid leadId);
    Task<IEnumerable<QuotationSummaryDTO>> GetQuotationsByAdvisorIdAsync(Guid advisorId);
    Task<IEnumerable<QuotationSummaryDTO>> GetAcceptedQuotationsByAdvisorIdAsync(Guid advisorId);
    Task<QuotationDTO> CreateQuotationAsync(QuotationCreateDTO dto);
    Task<QuotationDTO?> UpdateQuotationAsync(Guid id, QuotationUpdateDTO dto);
    Task<bool> DeleteQuotationAsync(Guid id);
    Task<QuotationDTO?> ChangeStatusAsync(Guid id, string status);
    Task<string> GenerateQuotationCodeAsync();
    Task<byte[]> GenerateQuotationPdfAsync(Guid quotationId);
}
