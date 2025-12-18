using GestionHogar.Controllers.Dtos;
using GestionHogar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionHogar.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    // GET: api/payments/reservation/{id}/schedule
    [HttpGet("reservation/{id:guid}/schedule")]
    public async Task<ActionResult<IEnumerable<PaymentDto>>> GetPaymentScheduleByReservation(
        Guid id
    )
    {
        try
        {
            var payments = await _paymentService.GetPaymentScheduleByReservationIdAsync(id);
            return Ok(payments);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error al obtener el cronograma de pagos para la reserva {ReservationId}",
                id
            );
            return StatusCode(500, $"Error al obtener el cronograma de pagos: {ex.Message}");
        }
    }
}
