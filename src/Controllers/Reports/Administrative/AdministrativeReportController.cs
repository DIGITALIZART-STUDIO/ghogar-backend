using GestionHogar.Controllers.Reports.Dto;
using GestionHogar.Model;
using GestionHogar.Services;
using GestionHogar.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionHogar.Controllers.Reports;

[ApiController]
[Authorize]
[Route("api/reports/administrative")]
public class AdministrativeReportController : ControllerBase
{
    private readonly IAdministrativeReportService _administrativeReportService;
    private readonly ILogger<AdministrativeReportController> _logger;

    public AdministrativeReportController(
        IAdministrativeReportService administrativeReportService,
        ILogger<AdministrativeReportController> logger
    )
    {
        _administrativeReportService = administrativeReportService;
        _logger = logger;
    }

    /// <summary>
    /// Reporte administrativo de créditos: contratos activos iniciados en el periodo,
    /// con cliente, DNI, proyecto y cuotas pendientes (paginado).
    /// </summary>
    [HttpGet("credits/paginated")]
    [AuthorizeCurrentUser("SuperAdmin", "Admin", "Manager", "FinanceManager")]
    public async Task<ActionResult<PaginatedResponseV2<CreditsReportItemDto>>> GetCreditsReportPaginated(
        [FromServices] PaginationService paginationService,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] Guid? projectId = null,
        [FromQuery] string? pendingInstallments = null,
        [FromQuery] string? orderBy = null
    )
    {
        var validationError = ValidateRangeAndPaging(from, to, page, pageSize);
        if (validationError != null)
        {
            return BadRequest(validationError);
        }

        try
        {
            var result = await _administrativeReportService.GetCreditsReportPaginatedAsync(
                from,
                to,
                page,
                pageSize,
                paginationService,
                search,
                projectId,
                pendingInstallments,
                orderBy
            );

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener el reporte administrativo de créditos");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Exporta el reporte de créditos a Excel, con los mismos filtros que la vista paginada.
    /// </summary>
    [HttpGet("credits/excel")]
    [AuthorizeCurrentUser("SuperAdmin", "Admin", "Manager", "FinanceManager")]
    public async Task<IActionResult> ExportCreditsReport(
        [FromServices] IExcelExportService excelExportService,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] string? search = null,
        [FromQuery] Guid? projectId = null,
        [FromQuery] string? pendingInstallments = null
    )
    {
        if (from > to)
        {
            return BadRequest("El parámetro 'from' no puede ser mayor que 'to'.");
        }

        try
        {
            var fileBytes = await _administrativeReportService.ExportCreditsReportAsync(
                from,
                to,
                excelExportService,
                search,
                projectId,
                pendingInstallments
            );

            return File(
                fileBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"reporte-creditos-{from:yyyyMMdd}-{to:yyyyMMdd}.xlsx"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al exportar el reporte administrativo de créditos");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// PDF de estado de cuenta de un crédito: datos completos del cliente,
    /// cronograma de cuotas y pagos registrados.
    /// </summary>
    [HttpGet("credits/{contractId:guid}/pdf")]
    [AuthorizeCurrentUser("SuperAdmin", "Admin", "Manager", "FinanceManager")]
    public async Task<IActionResult> ExportCreditStatementPdf(Guid contractId)
    {
        try
        {
            var file = await _administrativeReportService.GenerateCreditStatementPdfAsync(contractId);
            return File(file.Bytes, "application/pdf", file.FileName);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error al generar el PDF de estado de cuenta del contrato {ContractId}",
                contractId
            );
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Reporte administrativo de ingresos: vouchers registrados en el periodo,
    /// incluyendo pagos de contratos y de separaciones (paginado + resumen).
    /// </summary>
    [HttpGet("income/paginated")]
    [AuthorizeCurrentUser("SuperAdmin", "Admin", "Manager", "FinanceManager")]
    public async Task<ActionResult<IncomeReportResponse>> GetIncomeReportPaginated(
        [FromServices] PaginationService paginationService,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] Guid? projectId = null,
        [FromQuery] string? orderBy = null
    )
    {
        var validationError = ValidateRangeAndPaging(from, to, page, pageSize);
        if (validationError != null)
        {
            return BadRequest(validationError);
        }

        try
        {
            var result = await _administrativeReportService.GetIncomeReportPaginatedAsync(
                from,
                to,
                page,
                pageSize,
                paginationService,
                projectId,
                orderBy
            );

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener el reporte administrativo de ingresos");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Exporta el reporte de ingresos a Excel, con los mismos filtros que la vista paginada.
    /// </summary>
    [HttpGet("income/excel")]
    [AuthorizeCurrentUser("SuperAdmin", "Admin", "Manager", "FinanceManager")]
    public async Task<IActionResult> ExportIncomeReport(
        [FromServices] IExcelExportService excelExportService,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? projectId = null
    )
    {
        if (from > to)
        {
            return BadRequest("El parámetro 'from' no puede ser mayor que 'to'.");
        }

        try
        {
            var fileBytes = await _administrativeReportService.ExportIncomeReportAsync(
                from,
                to,
                excelExportService,
                projectId
            );

            return File(
                fileBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"reporte-ingresos-{from:yyyyMMdd}-{to:yyyyMMdd}.xlsx"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al exportar el reporte administrativo de ingresos");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    private static string? ValidateRangeAndPaging(DateOnly from, DateOnly to, int page, int pageSize)
    {
        if (from > to)
        {
            return "El parámetro 'from' no puede ser mayor que 'to'.";
        }

        if (page < 1)
        {
            return "La página debe ser mayor a 0";
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return "El tamaño de página debe estar entre 1 y 100";
        }

        return null;
    }
}
