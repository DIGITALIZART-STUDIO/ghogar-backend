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

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
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
            return StatusCode(500, $"Error al obtener el cronograma de pagos: {ex.Message}");
        }
    }
}
