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

    public ReservationsController(IReservationService reservationService)
    {
        _reservationService = reservationService;
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
}
