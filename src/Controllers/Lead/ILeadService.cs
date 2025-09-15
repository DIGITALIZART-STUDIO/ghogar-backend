using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;

namespace GestionHogar.Services;

public interface ILeadService
{
    Task<IEnumerable<Lead>> GetAllLeadsAsync();
    Task<PaginatedResponseV2<Lead>> GetAllLeadsPaginatedAsync(
        int page,
        int pageSize,
        PaginationService paginationService
    );
    Task<Lead?> GetLeadByIdAsync(Guid id);
    Task<Lead> CreateLeadAsync(Lead lead);
    Task<Lead?> UpdateLeadAsync(Guid id, Lead lead);
    Task<bool> DeleteLeadAsync(Guid id);
    Task<bool> ActivateLeadAsync(Guid id);
    Task<IEnumerable<Lead>> GetInactiveLeadsAsync();
    Task<IEnumerable<Lead>> GetLeadsByClientIdAsync(Guid clientId);
    Task<IEnumerable<Lead>> GetLeadsByAssignedToIdAsync(Guid userId);
    Task<PaginatedResponseV2<Lead>> GetLeadsByAssignedToIdPaginatedAsync(
        Guid userId,
        int page,
        int pageSize,
        PaginationService paginationService
    );
    Task<IEnumerable<Lead>> GetLeadsByStatusAsync(LeadStatus status);
    Task<IEnumerable<LeadSummaryDto>> GetAssignedLeadsSummaryAsync(Guid assignedToId);
    Task<IEnumerable<LeadSummaryDto>> GetAvailableLeadsForQuotationByUserAsync(
        Guid currentUserId,
        Guid? excludeQuotationId = null,
        IList<string>? currentUserRoles = null
    );
    Task<PaginatedResponseV2<LeadSummaryDto>> GetAvailableLeadsForQuotationPaginatedAsync(
        Guid currentUserId,
        IList<string> currentUserRoles,
        int page,
        int pageSize,
        string? search = null,
        string? orderBy = null,
        string? orderDirection = "asc",
        string? preselectedId = null
    );
    Task<IEnumerable<UserSummaryDto>> GetUsersSummaryAsync();
    Task<PaginatedResponseV2<UserSummaryDto>> GetUsersSummaryPaginatedAsync(
        int page,
        int pageSize,
        string? search = null,
        string? orderBy = null,
        string? orderDirection = "asc",
        string? preselectedId = null
    );
    Task<Lead?> ChangeLeadStatusAsync(Guid id, LeadStatus status, LeadCompletionReason? reason);

    // Método para generar código único de Lead
    Task<string> GenerateLeadCodeAsync();

    // Nuevos métodos para reciclaje y expiración
    Task<Lead?> RecycleLeadAsync(Guid id, Guid userId);
    Task<IEnumerable<Lead>> GetExpiredLeadsAsync();
    Task<int> CheckAndUpdateExpiredLeadsAsync();
    Task<byte[]> ExportLeadsToExcelAsync(IExcelExportService excelExportService);
}
