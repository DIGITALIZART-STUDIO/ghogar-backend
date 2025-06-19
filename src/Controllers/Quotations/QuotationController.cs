using GestionHogar.Dtos;
using GestionHogar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionHogar.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class QuotationsController : ControllerBase
{
    private readonly IQuotationService _quotationService;
    private readonly ILogger<QuotationsController> _logger;

    public QuotationsController(
        IQuotationService quotationService,
        ILogger<QuotationsController> logger
    )
    {
        _quotationService = quotationService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<QuotationDTO>>> GetAllQuotations()
    {
        try
        {
            var quotations = await _quotationService.GetAllQuotationsAsync();
            return Ok(quotations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener cotizaciones");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<QuotationDTO>> GetQuotationById(Guid id)
    {
        try
        {
            var quotation = await _quotationService.GetQuotationByIdAsync(id);
            if (quotation == null)
                return NotFound($"Cotización con ID {id} no encontrada");

            return Ok(quotation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener cotización");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpGet("lead/{leadId:guid}")]
    public async Task<ActionResult<IEnumerable<QuotationDTO>>> GetQuotationsByLeadId(Guid leadId)
    {
        try
        {
            var quotations = await _quotationService.GetQuotationsByLeadIdAsync(leadId);
            return Ok(quotations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener cotizaciones por lead");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpGet("advisor/{advisorId:guid}")]
    public async Task<ActionResult<IEnumerable<QuotationSummaryDTO>>> GetQuotationsByAdvisor(
        Guid advisorId
    )
    {
        try
        {
            var quotations = await _quotationService.GetQuotationsByAdvisorIdAsync(advisorId);
            return Ok(quotations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener cotizaciones por asesor");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpPost]
    public async Task<ActionResult<QuotationDTO>> CreateQuotation(QuotationCreateDTO dto)
    {
        try
        {
            var quotation = await _quotationService.CreateQuotationAsync(dto);
            return CreatedAtAction(nameof(GetQuotationById), new { id = quotation.Id }, quotation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear cotización");
            return StatusCode(500, ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<QuotationDTO>> UpdateQuotation(Guid id, QuotationUpdateDTO dto)
    {
        try
        {
            var quotation = await _quotationService.UpdateQuotationAsync(id, dto);
            if (quotation == null)
                return NotFound($"Cotización con ID {id} no encontrada");

            return Ok(quotation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar cotización");
            return StatusCode(500, ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteQuotation(Guid id)
    {
        try
        {
            var result = await _quotationService.DeleteQuotationAsync(id);
            if (!result)
                return NotFound($"Cotización con ID {id} no encontrada");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar cotización");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<QuotationDTO>> ChangeStatus(
        Guid id,
        QuotationStatusDTO statusDto
    )
    {
        try
        {
            var quotation = await _quotationService.ChangeStatusAsync(id, statusDto.Status);
            if (quotation == null)
                return NotFound($"Cotización con ID {id} no encontrada o estado inválido");

            return Ok(quotation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar estado de cotización");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpGet("generate-code")]
    public async Task<ActionResult<string>> GenerateQuotationCode()
    {
        try
        {
            var code = await _quotationService.GenerateQuotationCodeAsync();
            return Ok(new { code });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar código de cotización");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpGet("{id:guid}/pdf")]
    public async Task<ActionResult> GenerateQuotationPdf(Guid id)
    {
        try
        {
            var pdfBytes = await _quotationService.GenerateQuotationPdfAsync(id);

            return File(pdfBytes, "application/pdf", $"cotizacion-{id}.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar PDF de cotización");
            return StatusCode(500, "Error interno del servidor");
        }
    }
}
