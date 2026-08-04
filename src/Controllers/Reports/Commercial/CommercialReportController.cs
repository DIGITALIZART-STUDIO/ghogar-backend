using GestionHogar.Controllers.Reports.Dto;
using GestionHogar.Model;
using GestionHogar.Services;
using GestionHogar.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionHogar.Controllers.Reports;

[ApiController]
[Authorize]
[Route("api/reports/commercial")]
public class CommercialReportController : ControllerBase
{
    private readonly ICommercialReportService _commercialReportService;
    private readonly ILogger<CommercialReportController> _logger;

    private static readonly string[] AllowedRoles =
    {
        "SuperAdmin",
        "Admin",
        "Supervisor",
        "Manager",
        "CommercialManager",
    };

    public CommercialReportController(
        ICommercialReportService commercialReportService,
        ILogger<CommercialReportController> logger
    )
    {
        _commercialReportService = commercialReportService;
        _logger = logger;
    }

    /// <summary>
    /// Reporte comercial de actividad de asesores: leads ingresados en el periodo seleccionado,
    /// con estado, medio de captación, asesor asignado y actividad de tareas (paginado).
    /// </summary>
    [HttpGet("advisor-activity/paginated")]
    [AuthorizeCurrentUser("SuperAdmin", "Admin", "Supervisor", "Manager", "CommercialManager")]
    public async Task<ActionResult<PaginatedResponseV2<AdvisorActivityReportItemDto>>> GetAdvisorActivityReportPaginated(
        [FromServices] PaginationService paginationService,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] Guid? advisorId = null,
        [FromQuery] LeadStatus[]? status = null,
        [FromQuery] LeadCaptureSource[]? captureSource = null,
        [FromQuery] bool? hasAdvisor = null,
        [FromQuery] Guid? projectId = null,
        [FromQuery] string? orderBy = null
    )
    {
        if (from > to)
        {
            return BadRequest("El parámetro 'from' no puede ser mayor que 'to'.");
        }

        if (page < 1)
        {
            return BadRequest("La página debe ser mayor a 0");
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return BadRequest("El tamaño de página debe estar entre 1 y 100");
        }

        try
        {
            var currentUserId = User.GetCurrentUserIdOrThrow();
            var currentUserRoles = User.GetCurrentUserRoles().ToList();

            var result = await _commercialReportService.GetAdvisorActivityReportPaginatedAsync(
                from,
                to,
                page,
                pageSize,
                paginationService,
                currentUserId,
                currentUserRoles,
                advisorId,
                status,
                captureSource,
                hasAdvisor,
                projectId,
                orderBy
            );

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized("No se pudo identificar al usuario actual");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener el reporte de actividad de asesores");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Detalle de tareas de un lead específico, usado al expandir una fila del reporte.
    /// </summary>
    [HttpGet("advisor-activity/{leadId:guid}/tasks")]
    [AuthorizeCurrentUser("SuperAdmin", "Admin", "Supervisor", "Manager", "CommercialManager")]
    public async Task<ActionResult<List<AdvisorActivityTaskDto>>> GetAdvisorActivityLeadTasks(Guid leadId)
    {
        try
        {
            var currentUserId = User.GetCurrentUserIdOrThrow();
            var currentUserRoles = User.GetCurrentUserRoles().ToList();

            var tasks = await _commercialReportService.GetLeadTasksForReportAsync(
                leadId,
                currentUserId,
                currentUserRoles
            );

            return Ok(tasks);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized("No se pudo identificar al usuario actual");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener las tareas del lead {LeadId} para el reporte", leadId);
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Exporta el reporte de actividad de asesores a Excel, con los mismos filtros que la vista paginada.
    /// </summary>
    [HttpGet("advisor-activity/excel")]
    [AuthorizeCurrentUser("SuperAdmin", "Admin", "Supervisor", "Manager", "CommercialManager")]
    public async Task<IActionResult> ExportAdvisorActivityReport(
        [FromServices] IExcelExportService excelExportService,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? advisorId = null,
        [FromQuery] LeadStatus[]? status = null,
        [FromQuery] LeadCaptureSource[]? captureSource = null,
        [FromQuery] bool? hasAdvisor = null,
        [FromQuery] Guid? projectId = null
    )
    {
        if (from > to)
        {
            return BadRequest("El parámetro 'from' no puede ser mayor que 'to'.");
        }

        try
        {
            var currentUserId = User.GetCurrentUserIdOrThrow();
            var currentUserRoles = User.GetCurrentUserRoles().ToList();

            var fileBytes = await _commercialReportService.ExportAdvisorActivityReportAsync(
                from,
                to,
                excelExportService,
                currentUserId,
                currentUserRoles,
                advisorId,
                status,
                captureSource,
                hasAdvisor,
                projectId
            );

            return File(
                fileBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"reporte-actividad-asesores-{from:yyyyMMdd}-{to:yyyyMMdd}.xlsx"
            );
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized("No se pudo identificar al usuario actual");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al exportar el reporte de actividad de asesores");
            return StatusCode(500, "Error interno del servidor");
        }
    }
}
