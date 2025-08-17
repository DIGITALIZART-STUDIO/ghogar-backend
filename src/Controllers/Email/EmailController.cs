using GestionHogar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionHogar.Controllers;

[ApiController]
[Route("/api/[controller]")]
[Authorize]
public class EmailController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly ILogger<EmailController> _logger;

    public EmailController(
        IEmailService emailService,
        IEmailTemplateService emailTemplateService,
        ILogger<EmailController> logger
    )
    {
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
        _logger = logger;
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendEmail([FromBody] SendEmailRequest request)
    {
        try
        {
            var success = await _emailService.SendEmailAsync(
                request.To,
                request.Subject,
                request.Content,
                request.Context
            );

            if (success)
            {
                return Ok(new { message = "Email sent successfully" });
            }
            else
            {
                return BadRequest(new { message = "Failed to send email" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpPost("test")]
    public async Task<IActionResult> SendTestEmail([FromBody] TestEmailRequest request)
    {
        try
        {
            var content =
                @"
<div class=""email-content"">
    <h2>¡Hola {{name}}!</h2>
    <p>Este es un correo de prueba desde <strong>{{business}}</strong>.</p>
    <p>Puedes usar variables dinámicas como:</p>
    <ul>
        <li>Tu nombre: {{name}}</li>
        <li>Tu email: {{email}}</li>
        <li>Fecha actual: {{currentDate}}</li>
    </ul>
    <p>También puedes incluir botones:</p>
    <a href=""{{url}}"" class=""btn"">Visitar nuestro sitio web</a>
    <p>¡Gracias por usar nuestro servicio!</p>
</div>";

            var context = new Dictionary<string, object>
            {
                ["name"] = request.Name,
                ["email"] = request.Email,
                ["currentDate"] = DateTime.Now.ToString("dd/MM/yyyy"),
            };

            var success = await _emailService.SendEmailAsync(
                request.Email,
                "Correo de Prueba - Gestion Hogar",
                content,
                context
            );

            if (success)
            {
                return Ok(new { message = "Test email sent successfully" });
            }
            else
            {
                return BadRequest(new { message = "Failed to send test email" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending test email");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpPost("welcome")]
    public async Task<IActionResult> SendWelcomeEmail([FromBody] WelcomeEmailRequest request)
    {
        try
        {
            var content = _emailTemplateService.GetWelcomeTemplate(
                request.Name,
                request.Email,
                request.Password,
                request.WebAdminUrl
            );

            var success = await _emailService.SendEmailAsync(
                request.Email,
                $"Bienvenido a Gestion Hogar: {request.Name}",
                content
            );

            if (success)
            {
                return Ok(new { message = "Welcome email sent successfully" });
            }
            else
            {
                return BadRequest(new { message = "Failed to send welcome email" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending welcome email");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpPost("quotation")]
    public async Task<IActionResult> SendQuotationEmail([FromBody] QuotationEmailRequest request)
    {
        try
        {
            var content = _emailTemplateService.GetQuotationTemplate(
                request.ClientName,
                request.QuotationNumber,
                request.Amount,
                request.ProjectName
            );

            var success = await _emailService.SendEmailAsync(
                request.Email,
                $"Cotización #{request.QuotationNumber} - {request.ProjectName}",
                content
            );

            if (success)
            {
                return Ok(new { message = "Quotation email sent successfully" });
            }
            else
            {
                return BadRequest(new { message = "Failed to send quotation email" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending quotation email");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpPost("reservation")]
    public async Task<IActionResult> SendReservationEmail(
        [FromBody] ReservationEmailRequest request
    )
    {
        try
        {
            var content = _emailTemplateService.GetReservationTemplate(
                request.ClientName,
                request.LotNumber,
                request.ProjectName,
                request.Amount
            );

            var success = await _emailService.SendEmailAsync(
                request.Email,
                $"Reserva Confirmada - Lote {request.LotNumber}",
                content
            );

            if (success)
            {
                return Ok(new { message = "Reservation email sent successfully" });
            }
            else
            {
                return BadRequest(new { message = "Failed to send reservation email" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending reservation email");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpPost("payment-reminder")]
    public async Task<IActionResult> SendPaymentReminderEmail(
        [FromBody] PaymentReminderEmailRequest request
    )
    {
        try
        {
            var content = _emailTemplateService.GetPaymentReminderTemplate(
                request.ClientName,
                request.PaymentNumber,
                request.Amount,
                request.DueDate
            );

            var success = await _emailService.SendEmailAsync(
                request.Email,
                $"Recordatorio de Pago #{request.PaymentNumber}",
                content
            );

            if (success)
            {
                return Ok(new { message = "Payment reminder email sent successfully" });
            }
            else
            {
                return BadRequest(new { message = "Failed to send payment reminder email" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending payment reminder email");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}

public class SendEmailRequest
{
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Dictionary<string, object>? Context { get; set; }
}

public class TestEmailRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class WelcomeEmailRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string WebAdminUrl { get; set; } = string.Empty;
}

public class QuotationEmailRequest
{
    public string ClientName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string QuotationNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string ProjectName { get; set; } = string.Empty;
}

public class ReservationEmailRequest
{
    public string ClientName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string LotNumber { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class PaymentReminderEmailRequest
{
    public string ClientName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PaymentNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
}
