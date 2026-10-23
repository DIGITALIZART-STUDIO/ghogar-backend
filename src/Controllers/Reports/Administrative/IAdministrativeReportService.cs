using GestionHogar.Controllers.Reports;
using GestionHogar.Controllers.Reports.Dto;
using GestionHogar.Model;
using GestionHogar.Services;

namespace GestionHogar.Services;

public interface IAdministrativeReportService
{
    Task<PaginatedResponseV2<CreditsReportItemDto>> GetCreditsReportPaginatedAsync(
        DateOnly from,
        DateOnly to,
        int page,
        int pageSize,
        PaginationService paginationService,
        string? search = null,
        Guid? projectId = null,
        string? pendingInstallments = null,
        string? orderBy = null
    );

    Task<byte[]> ExportCreditsReportAsync(
        DateOnly from,
        DateOnly to,
        IExcelExportService excelExportService,
        string? search = null,
        Guid? projectId = null,
        string? pendingInstallments = null
    );

    Task<CreditStatementPdfFile> GenerateCreditStatementPdfAsync(Guid contractId);

    Task<IncomeReportResponse> GetIncomeReportPaginatedAsync(
        DateOnly from,
        DateOnly to,
        int page,
        int pageSize,
        PaginationService paginationService,
        Guid? projectId = null,
        string? orderBy = null
    );

    Task<byte[]> ExportIncomeReportAsync(
        DateOnly from,
        DateOnly to,
        IExcelExportService excelExportService,
        Guid? projectId = null
    );
}
