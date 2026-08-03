using GestionHogar.Controllers.Reports.Dto;
using GestionHogar.Model;
using GestionHogar.Services;

namespace GestionHogar.Services;

public interface ICommercialReportService
{
    /// <summary>
    /// Reporte comercial de actividad de asesores: leads ingresados en un periodo,
    /// con su estado, medio de captación, asesor asignado y actividad de tareas.
    /// </summary>
    Task<PaginatedResponseV2<AdvisorActivityReportItemDto>> GetAdvisorActivityReportPaginatedAsync(
        DateOnly from,
        DateOnly to,
        int page,
        int pageSize,
        PaginationService paginationService,
        Guid currentUserId,
        IList<string> currentUserRoles,
        Guid? advisorId = null,
        LeadStatus[]? status = null,
        LeadCaptureSource[]? captureSource = null,
        bool? hasAdvisor = null,
        Guid? projectId = null,
        string? orderBy = null
    );

    /// <summary>
    /// Detalle de tareas de un lead específico (usado al expandir una fila del reporte).
    /// </summary>
    Task<List<AdvisorActivityTaskDto>> GetLeadTasksForReportAsync(
        Guid leadId,
        Guid currentUserId,
        IList<string> currentUserRoles
    );

    /// <summary>
    /// Genera el archivo Excel del reporte con los mismos filtros que la vista paginada, sin paginar.
    /// </summary>
    Task<byte[]> ExportAdvisorActivityReportAsync(
        DateOnly from,
        DateOnly to,
        IExcelExportService excelExportService,
        Guid currentUserId,
        IList<string> currentUserRoles,
        Guid? advisorId = null,
        LeadStatus[]? status = null,
        LeadCaptureSource[]? captureSource = null,
        bool? hasAdvisor = null,
        Guid? projectId = null
    );
}
