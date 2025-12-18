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
    Task<QuotationDTO> CreateQuotationAsync(
        QuotationCreateDTO dto,
        Guid currentUserId,
        IEnumerable<string> currentUserRoles
    );
    Task<QuotationDTO?> UpdateQuotationAsync(Guid id, QuotationUpdateDTO dto);
    Task<bool> DeleteQuotationAsync(Guid id);
    Task<QuotationDTO?> ChangeStatusAsync(Guid id, string status);
    Task<string> GenerateQuotationCodeAsync();
    Task<byte[]> GenerateQuotationPdfAsync(Guid quotationId);

    // Métodos OTP
    Task<SendOtpResponseDto> SendOtpToUserAsync(Guid userId, Guid requestedByUserId);
    Task<VerifyOtpResponseDto> VerifyOtpAsync(Guid userId, string otpCode);

    // Nuevos métodos para validación de leads y clientes
    Task<bool> LeadExistsAsync(Guid leadId);
    Task<bool> ClientExistsAsync(Guid clientId);
    Task<Guid> CreateLeadFromClientAsync(Guid clientId, Guid assignedToUserId);
}
