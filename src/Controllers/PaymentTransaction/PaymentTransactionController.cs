using GestionHogar.Dtos;
using GestionHogar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionHogar.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentTransactionController : ControllerBase
{
    private readonly IPaymentTransactionService _service;
    private readonly ILogger<PaymentTransactionController> _logger;

    public PaymentTransactionController(
        IPaymentTransactionService service,
        ILogger<PaymentTransactionController> logger
    )
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PaymentTransactionDTO>>> GetAll()
    {
        try
        {
            var result = await _service.GetAllAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener transacciones de pago");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentTransactionDTO>> GetById(Guid id)
    {
        try
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null)
                return NotFound($"Transacción con ID {id} no encontrada");
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener transacción de pago");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpGet("by-reservation/{reservationId:guid}")]
    public async Task<ActionResult<IEnumerable<PaymentTransactionDTO>>> GetByReservationId(
        Guid reservationId
    )
    {
        try
        {
            var result = await _service.GetByReservationIdAsync(reservationId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener transacciones por ReservationId");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpGet("quota-status/by-reservation/{reservationId:guid}/{excludeTransactionId:guid?}")]
    public async Task<ActionResult<PaymentQuotaStatusDTO>> GetQuotaStatus(
        Guid reservationId,
        Guid? excludeTransactionId
    )
    {
        try
        {
            var result = await _service.GetQuotaStatusByReservationAsync(
                reservationId,
                excludeTransactionId
            );
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener estado de cuotas");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpPost]
    public async Task<ActionResult<PaymentTransactionDTO>> Create(
        [FromForm] PaymentTransactionCreateDTO dto,
        [FromForm] IFormFile? comprobanteFile = null
    )
    {
        try
        {
            var result = await _service.CreateAsync(dto, comprobanteFile);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear transacción de pago");
            return StatusCode(500, ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PaymentTransactionDTO>> Update(
        Guid id,
        [FromForm] PaymentTransactionUpdateDTO dto,
        [FromForm] IFormFile? comprobanteFile = null
    )
    {
        try
        {
            var result = await _service.UpdateAsync(id, dto, comprobanteFile);
            if (result == null)
                return NotFound($"Transacción con ID {id} no encontrada");
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar transacción de pago");
            return StatusCode(500, ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted)
                return NotFound($"Transacción con ID {id} no encontrada");
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar transacción de pago");
            return StatusCode(500, "Error interno del servidor");
        }
    }
}
