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
    private readonly IEmailUrlService _emailUrlService;
    private readonly ILogger<EmailController> _logger;

    public EmailController(
        IEmailService emailService,
        IEmailTemplateService emailTemplateService,
        IEmailUrlService emailUrlService,
        ILogger<EmailController> logger
    )
    {
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
        _emailUrlService = emailUrlService;
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
    <h1>¡Hola <span class=""highlight"">{{name}}</span>!</h1>
    <p>Este es un correo de prueba desde <strong class=""highlight"">{{business}}</strong>.</p>
    
    <div class=""info-box"">
        <h3>Variables disponibles:</h3>
        <ul>
            <li><strong>Tu nombre:</strong> {{name}}</li>
            <li><strong>Tu email:</strong> {{email}}</li>
            <li><strong>Fecha actual:</strong> {{currentDate}}</li>
            <li><strong>Teléfono:</strong> {{phone}}</li>
            <li><strong>Dirección:</strong> {{address}}</li>
        </ul>
    </div>
    
    <p>También puedes incluir botones estilizados:</p>
    <div style=""text-align: center; margin: 30px 0;"">
        <a href=""{{url}}"" class=""btn"">Visitar nuestro sitio web</a>
    </div>
    
    <div class=""divider""></div>
    
    <p>¡Gracias por usar nuestro servicio!</p>";

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

    [HttpGet("test")]
    public async Task<IActionResult> Test()
    {
        try
        {
            // Contenido simple para probar
            var content =
                @"
    <h1>¡Hola {{name}}!</h1>
    <p>Bienvenido a <strong>{{business}}</strong></p>
    <p>Tu logo está en: {{logoUrl}}</p>
    <p>Tu teléfono es: {{phone}}</p>
    <p>Tu URL es: {{url}}</p>";

            var context = new Dictionary<string, object> { ["name"] = "Juan Pérez" };

            // Generar el HTML del correo
            var emailHtml = await _emailService.GenerateEmailHtmlAsync(content, context);

            return Content(emailHtml, "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in test");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    [HttpGet("test-otp")]
    public async Task<IActionResult> TestOtp()
    {
        try
        {
            // Contenido del OTP para probar
            var content =
                @"
        <h1 style=""color: #1a1a1a; font-weight: 700; font-size: 28px; margin-bottom: 25px; text-align: center;"">
            Código de Autorización
        </h1>
        
        <p style=""font-size: 16px; color: #1a1a1a; margin-bottom: 20px;"">
            Estimado(a) <span class=""highlight"">Juan Pérez</span>,
        </p>
        
        <div class=""info-box"">
            <p style=""font-size: 15px; color: #1a1a1a; margin-bottom: 15px;"">
                El usuario <span class=""highlight"">María García</span> ha solicitado autorización para desbloquear un descuento especial en la plataforma <span class=""highlight"">{{business}}</span>.
            </p>
        </div>
        
        <div style=""text-align: center; margin: 40px 0;"">
            <div style=""display: inline-block; background: linear-gradient(135deg, #1a1a1a 0%, #333333 100%); border: 3px solid #ffd700; border-radius: 12px; padding: 30px 50px; box-shadow: 0 8px 25px rgba(255, 215, 0, 0.3);"">
                <div style=""font-size: 42px; font-weight: 800; letter-spacing: 8px; color: #ffd700; font-family: 'Montserrat', 'Segoe UI Mono', monospace; text-shadow: 0 2px 4px rgba(0,0,0,0.3);"">
                    123-456
                </div>
            </div>
        </div>
        
        <div class=""info-box"">
            <h3 style=""color: #1a1a1a; font-weight: 600; margin-bottom: 15px;"">
                Información Importante:
            </h3>
            <ul style=""color: #333333; font-size: 14px; margin: 0; padding-left: 20px;"">
                <li style=""margin-bottom: 8px;"">Este código es válido por <span class=""highlight"">5 minutos</span>.</li>
                <li style=""margin-bottom: 8px;"">Solo puede ser utilizado <strong>una vez</strong>.</li>
                <li style=""margin-bottom: 8px;"">No comparta este código con terceros.</li>
                <li style=""margin-bottom: 8px;"">Si usted no solicitó este código, por favor ignore este mensaje.</li>
            </ul>
        </div>
        
        <div class=""divider""></div>
        
        <p style=""font-size: 14px; color: #666666; text-align: center; margin-top: 25px;"">
            Si tiene alguna consulta, comuníquese con el equipo de soporte de <span class=""highlight"">{{business}}</span>.
        </p>
        
        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{{url}}"" class=""btn"">Acceder a la Plataforma</a>
        </div>";

            // Generar el HTML del correo
            var emailHtml = await _emailService.GenerateEmailHtmlAsync(content);

            return Content(emailHtml, "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in test OTP");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
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
