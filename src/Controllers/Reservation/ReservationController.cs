using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;
using GestionHogar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionHogar.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ReservationsController : ControllerBase
{
    private readonly IReservationService _reservationService;
    private readonly WordTemplateService _wordTemplateService;
    private readonly SofficeConverterService _sofficeConverterService;

    public ReservationsController(
        IReservationService reservationService,
        WordTemplateService wordTemplateService,
        SofficeConverterService sofficeConverterService
    )
    {
        _reservationService = reservationService;
        _wordTemplateService = wordTemplateService;
        _sofficeConverterService = sofficeConverterService;
    }

    // GET: api/reservations
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReservationDto>>> GetReservations()
    {
        var reservations = await _reservationService.GetAllReservationsAsync();
        return Ok(reservations);
    }

    [HttpGet("canceled")]
    public async Task<
        ActionResult<IEnumerable<ReservationWithPaymentsDto>>
    > GetAllCanceledReservations()
    {
        var reservations = await _reservationService.GetAllCanceledReservationsAsync();
        return Ok(reservations);
    }

    [HttpGet("canceled/pending-validation/paginated")]
    public async Task<
        ActionResult<PaginatedResponseV2<ReservationDto>>
    > GetAllCanceledPendingValidationReservationsPaginated(
        [FromServices] PaginationService paginationService,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] Guid? projectId = null
    )
    {
        var result =
            await _reservationService.GetAllCanceledPendingValidationReservationsPaginatedAsync(
                page,
                pageSize,
                paginationService,
                projectId
            );
        return Ok(result);
    }

    [HttpGet("canceled/paginated")]
    public async Task<
        ActionResult<PaginatedResponseV2<ReservationWithPaymentsDto>>
    > GetAllCanceledReservationsPaginated(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] Guid? projectId = null
    )
    {
        var result = await _reservationService.GetAllCanceledReservationsPaginatedAsync(
            page,
            pageSize,
            projectId
        );
        return Ok(result);
    }

    [HttpGet("pending-payments/paginated")]
    public async Task<
        ActionResult<PaginatedResponseV2<ReservationWithPendingPaymentsDto>>
    > GetAllReservationsWithPendingPaymentsPaginated(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] Guid? projectId = null
    )
    {
        try
        {
            var result =
                await _reservationService.GetAllReservationsWithPendingPaymentsPaginatedAsync(
                    page,
                    pageSize,
                    projectId
                );
            return Ok(result);
        }
        catch (Exception ex)
        {
            // _logger.LogError(ex, "Error al obtener reservas con cuotas pendientes"); // Assuming _logger is available
            return StatusCode(500, "Error interno del servidor");
        }
    }

    // GET: api/reservations/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<ReservationDto>> GetReservation(Guid id)
    {
        var reservation = await _reservationService.GetReservationByIdAsync(id);
        if (reservation == null)
            return NotFound();

        return Ok(reservation);
    }

    // POST: api/reservations
    [HttpPost]
    public async Task<ActionResult<ReservationDto>> CreateReservation(
        ReservationCreateDto reservationDto
    )
    {
        try
        {
            var createdReservation = await _reservationService.CreateReservationAsync(
                reservationDto
            );

            // Get the created reservation as DTO to return
            var createdReservationDto = await _reservationService.GetReservationByIdAsync(
                createdReservation.Id
            );

            return CreatedAtAction(
                nameof(GetReservation),
                new { id = createdReservation.Id },
                createdReservationDto
            );
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id}/toggle-validation-status")]
    public async Task<ActionResult> ToggleContractValidationStatus(Guid id)
    {
        var success = await _reservationService.ToggleContractValidationStatusAsync(id);
        if (!success)
            return NotFound();

        return NoContent();
    }

    // PATCH: api/reservations/{id}
    [HttpPatch("{id}")]
    public async Task<ActionResult<ReservationDto>> UpdateReservation(
        Guid id,
        ReservationUpdateDto reservationDto
    )
    {
        try
        {
            var updatedReservation = await _reservationService.UpdateReservationAsync(
                id,
                reservationDto
            );

            if (updatedReservation == null)
                return NotFound();

            return Ok(updatedReservation);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // DELETE: api/reservations/{id}
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteReservation(Guid id)
    {
        var success = await _reservationService.DeleteReservationAsync(id);
        if (!success)
            return NotFound();

        return NoContent();
    }

    // GET: api/reservations/client/{clientId}
    [HttpGet("client/{clientId}")]
    public async Task<ActionResult<IEnumerable<ReservationDto>>> GetReservationsByClient(
        Guid clientId
    )
    {
        var reservations = await _reservationService.GetReservationsByClientIdAsync(clientId);
        return Ok(reservations);
    }

    // GET: api/reservations/quotation/{quotationId}
    [HttpGet("quotation/{quotationId}")]
    public async Task<ActionResult<IEnumerable<ReservationDto>>> GetReservationsByQuotation(
        Guid quotationId
    )
    {
        var reservations = await _reservationService.GetReservationsByQuotationIdAsync(quotationId);
        return Ok(reservations);
    }

    // PUT: api/reservations/{id}/status
    [HttpPut("{id}/status")]
    public async Task<ActionResult<ReservationDto>> ChangeReservationStatus(
        Guid id,
        ReservationStatusDto statusDto
    )
    {
        try
        {
            var updatedReservation = await _reservationService.ChangeStatusAsync(
                id,
                statusDto.Status
            );

            if (updatedReservation == null)
                return NotFound();

            return Ok(updatedReservation);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error al cambiar el estado de la separacion: {ex.Message}");
        }
    }

    // GET: api/reservations/{id}/pdf
    [HttpGet("{id}/pdf")]
    public async Task<ActionResult> GenerateReservationPdf(Guid id)
    {
        try
        {
            var pdfBytes = await _reservationService.GenerateReservationPdfAsync(id);
            return File(pdfBytes, "application/pdf", $"separacion-{id}.pdf");
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error al generar PDF de separación: {ex.Message}");
        }
    }

    // GET: api/reservations/{id}/contract/pdf
    [HttpGet("{id}/contract/pdf")]
    public async Task<ActionResult> GenerateContractPdf(Guid id)
    {
        try
        {
            var pdfBytes = await _reservationService.GenerateContractPdfAsync(id);
            return File(pdfBytes, "application/pdf", $"contrato-{id}.pdf");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // GET: api/reservations/{id}/contract/docx
    [HttpGet("{id}/contract/docx")]
    public async Task<ActionResult> GenerateContractDocx(Guid id)
    {
        try
        {
            var docxBytes = await _reservationService.GenerateContractDocxAsync(id);
            return File(
                docxBytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                $"contrato-{id}.docx"
            );
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // GET: api/reservations/{id}/schedule/pdf
    [HttpGet("{id}/schedule/pdf")]
    public async Task<ActionResult> GenerateSchedulePdf(Guid id)
    {
        try
        {
            var pdfBytes = await _reservationService.GenerateSchedulePdfAsync(id);
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

    // GET: api/reservations/{id}/processed-payments/pdf
    [HttpGet("{id}/processed-payments/pdf")]
    public async Task<ActionResult> GenerateProcessedPaymentsPdf(Guid id)
    {
        try
        {
            var pdfBytes = await _reservationService.GenerateProcessedPaymentsPdfAsync(id);
            return File(pdfBytes, "application/pdf", $"pagos-realizados-{id}.pdf");
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error al generar PDF de pagos realizados: {ex.Message}");
        }
    }

    // GET: api/reservations/{id}/receipt/pdf
    [HttpGet("{id}/receipt/pdf")]
    public async Task<ActionResult> GenerateReceiptPdf(Guid id)
    {
        try
        {
            var pdfBytes = await _reservationService.GenerateReceiptPdfAsync(id);
            return File(pdfBytes, "application/pdf", $"recibo-{id}.pdf");
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error al generar PDF de recibo: {ex.Message}");
        }
    }
}
