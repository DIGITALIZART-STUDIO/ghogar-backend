using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;
using GestionHogar.Services;
using GestionHogar.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionHogar.Controllers;

[ApiController]
[Authorize]
[AuthorizeCurrentUser("SuperAdmin", "Admin", "Manager", "FinanceManager")]
[Route("api/[controller]")]
public class ContractsController(
    IContractService contractService,
    IContractBackfillService backfillService
) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedResponseV2<ContractDto>>> GetContracts(
        [FromServices] PaginationService paginationService,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] ContractStatus[]? status = null,
        [FromQuery] ContractOrigin[]? origin = null,
        [FromQuery] Guid? clientId = null,
        [FromQuery] Guid? projectId = null,
        [FromQuery] bool? overdue = null,
        [FromQuery] bool? hasBalance = null,
        [FromQuery] string? orderBy = null
    )
    {
        if (page < 1)
        {
            return BadRequest("La página debe ser mayor a 0");
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return BadRequest("El tamaño de página debe estar entre 1 y 100");
        }

        var result = await contractService.GetPaginatedAsync(
            page,
            pageSize,
            paginationService,
            search,
            status,
            origin,
            clientId,
            projectId,
            overdue,
            hasBalance,
            orderBy
        );
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContractDto>> GetContract(Guid id)
    {
        var contract = await contractService.GetByIdAsync(id);
        if (contract == null)
        {
            return NotFound();
        }

        return Ok(contract);
    }

    [HttpGet("{id:guid}/schedule")]
    public async Task<ActionResult<IEnumerable<PaymentDto>>> GetSchedule(Guid id)
    {
        var contract = await contractService.GetByIdAsync(id);
        if (contract == null)
        {
            return NotFound();
        }

        var schedule = await contractService.GetScheduleByIdAsync(id);
        return Ok(schedule);
    }

    [HttpPost]
    public async Task<ActionResult<ContractDto>> Create([FromBody] CreateContractDto dto)
    {
        try
        {
            var created = await contractService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetContract), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ContractDto>> Update(Guid id, [FromBody] UpdateContractDto dto)
    {
        try
        {
            var updated = await contractService.UpdateAsync(id, dto);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id:guid}/schedule/preview")]
    public async Task<ActionResult<IEnumerable<PaymentDto>>> PreviewSchedule(
        Guid id,
        [FromBody] ContractSchedulePreviewDto dto
    )
    {
        try
        {
            var schedule = await contractService.PreviewScheduleAsync(id, dto);
            return Ok(schedule);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:guid}/schedule")]
    public async Task<ActionResult<ContractDto>> UpdateSchedule(
        Guid id,
        [FromBody] UpdateContractScheduleDto dto
    )
    {
        try
        {
            var updated = await contractService.UpdateScheduleAsync(id, dto);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:guid}/rescind")]
    public async Task<ActionResult<ContractDto>> Rescind(Guid id)
    {
        try
        {
            var updated = await contractService.RescindAsync(id);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:guid}/toggle-validation-status")]
    public async Task<ActionResult<ContractDto>> ToggleValidationStatus(Guid id)
    {
        try
        {
            var updated = await contractService.ToggleValidationStatusAsync(id);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id:guid}/pdf")]
    public async Task<ActionResult> GeneratePdf(Guid id)
    {
        try
        {
            var pdfBytes = await contractService.GenerateContractPdfAsync(id);
            return File(pdfBytes, "application/pdf", $"contrato-{id}.pdf");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/docx")]
    public async Task<ActionResult> GenerateDocx(Guid id)
    {
        try
        {
            var docxBytes = await contractService.GenerateContractDocxAsync(id);
            return File(
                docxBytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                $"contrato-{id}.docx"
            );
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/schedule/pdf")]
    public async Task<ActionResult> GenerateSchedulePdf(Guid id)
    {
        try
        {
            var pdfBytes = await contractService.GenerateSchedulePdfAsync(id);
            return File(pdfBytes, "application/pdf", $"cronograma-{id}.pdf");
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error al generar PDF de cronograma: {ex.Message}");
        }
    }

    [HttpPost("backfill")]
    [AuthorizeCurrentUser("SuperAdmin", "Admin")]
    public async Task<ActionResult<ContractBackfillResultDto>> Backfill()
    {
        var result = await backfillService.BackfillFromReservationsAsync();
        return Ok(result);
    }
}
