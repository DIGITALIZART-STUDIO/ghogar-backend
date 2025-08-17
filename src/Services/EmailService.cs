using GestionHogar.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace GestionHogar.Services;

public interface IEmailService
{
    Task<bool> SendEmailAsync(EmailRequest request);
    Task<bool> SendEmailAsync(
        string to,
        string subject,
        string content,
        Dictionary<string, object>? context = null
    );
    Task<string> GenerateEmailHtmlAsync(string content, Dictionary<string, object>? context = null);
}

public class EmailRequest
{
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Dictionary<string, object>? Context { get; set; }
    public string? TemplateName { get; set; }
}

public class EmailService : IEmailService
{
    private readonly EmailConfiguration _emailConfig;
    private readonly BusinessInfo _businessInfo;
    private readonly ILogger<EmailService> _logger;
    private readonly IEmailUrlService _emailUrlService;

    public EmailService(
        IOptions<EmailConfiguration> emailConfig,
        IOptions<BusinessInfo> businessInfo,
        ILogger<EmailService> logger,
        IEmailUrlService emailUrlService
    )
    {
        _emailConfig = emailConfig.Value;
        _businessInfo = businessInfo.Value;
        _emailUrlService = emailUrlService;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(EmailRequest request)
    {
        try
        {
            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(_emailConfig.Username));
            email.To.Add(MailboxAddress.Parse(request.To));
            email.Subject = request.Subject;

            // Generar el contenido del email con template
            var emailContent = await GenerateEmailContentAsync(request.Content, request.Context);
            email.Body = new TextPart(TextFormat.Html) { Text = emailContent };

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(
                _emailConfig.Host,
                _emailConfig.Port,
                _emailConfig.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None
            );

            if (!_emailConfig.UseDefaultCredentials)
            {
                await smtp.AuthenticateAsync(_emailConfig.Username, _emailConfig.Password);
            }

            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);

            _logger.LogInformation("Email sent successfully to {To}", request.To);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", request.To);
            return false;
        }
    }

    public async Task<bool> SendEmailAsync(
        string to,
        string subject,
        string content,
        Dictionary<string, object>? context = null
    )
    {
        var request = new EmailRequest
        {
            To = to,
            Subject = subject,
            Content = content,
            Context = context,
        };

        return await SendEmailAsync(request);
    }

    public async Task<string> GenerateEmailHtmlAsync(
        string content,
        Dictionary<string, object>? context = null
    )
    {
        return await GenerateEmailContentAsync(content, context);
    }

    private async Task<string> GenerateEmailContentAsync(
        string content,
        Dictionary<string, object>? context = null
    )
    {
        // Combinar el contexto del negocio con el contexto personalizado
        var fullContext = new Dictionary<string, object>
        {
            ["business"] = _businessInfo.Business,
            ["url"] = _businessInfo.Url,
            ["phone"] = _businessInfo.Phone,
            ["address"] = _businessInfo.Address,
            ["contact"] = _businessInfo.Contact,
            ["logoUrl"] = _emailUrlService.GetLogoUrl(),
            ["year"] = DateTime.Now.Year,
        };

        if (context != null)
        {
            foreach (var item in context)
            {
                fullContext[item.Key] = item.Value;
            }
        }

        // Generar el template completo
        var template = await GenerateEmailTemplateAsync(content, fullContext);
        return template;
    }

    private Task<string> GenerateEmailTemplateAsync(
        string content,
        Dictionary<string, object> context
    )
    {
        // Reemplazar variables en el contenido primero
        var processedContent = ReplaceTemplateVariables(content, context);

        // Generar header y footer
        var header = GenerateEmailHeader();
        var footer = GenerateEmailFooter(context);

        // Crear el template HTML completo con variables ya procesadas
        var template =
            $@"
<!DOCTYPE html>
<html lang=""es"">
{header}
<body>
    <div class=""email-container"">
        <div class=""email-header"">
            <div class=""logo-container"">
                <img src=""{context.GetValueOrDefault("logoUrl", "")}"" alt=""{context.GetValueOrDefault("business", "")}"" class=""logo"">
            </div>
        </div>
        <div class=""email-content"">
            {processedContent}
        </div>
        {footer}
    </div>
</body>
</html>";

        return Task.FromResult(template);
    }

    private string GenerateEmailHeader()
    {
        return @"
<head>
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <meta name=""x-apple-disable-message-reformatting"" />
    <meta http-equiv=""Content-Type"" content=""text/html; charset=UTF-8"" />
    <meta name=""color-scheme"" content=""light dark"" />
    <meta name=""supported-color-schemes"" content=""light dark"" />
    <style>
        @import url('https://fonts.googleapis.com/css2?family=Montserrat:wght@400;500;700&display=swap');
        
        body {
            font-family: 'Montserrat', sans-serif;
            font-optical-sizing: auto;
            background-color: #fafafa;
            margin: 0;
            padding: 0;
            -webkit-font-smoothing: antialiased;
            color: #1a1a1a;
            line-height: 1.6;
        }
        .email-container {
            max-width: 600px;
            margin: 0 auto;
            background-color: #ffffff;
            border-radius: 10px;
            box-shadow: 0 4px 20px rgba(0,0,0,0.08);
            overflow: hidden;
        }
        .email-header {
            background-color: #000000;
            color: white;
            padding: 40px 20px 30px;
            text-align: center;
            position: relative;
        }
        .logo-container {
            margin-bottom: 20px;
        }
        .logo {
            max-width: 200px;
            height: auto;
            margin: 0 auto;
            display: block;
        }
        .email-content {
            padding: 40px 30px;
            line-height: 1.7;
            color: #1a1a1a;
        }
        .email-content h1 {
            color: #1a1a1a;
            font-weight: 700;
            font-size: 24px;
            margin-bottom: 20px;
        }
        .email-content h2 {
            color: #1a1a1a;
            font-weight: 600;
            font-size: 20px;
            margin-bottom: 15px;
        }
        .email-content h3 {
            color: #1a1a1a;
            font-weight: 500;
            font-size: 18px;
            margin-bottom: 12px;
        }
        .email-content p {
            margin-bottom: 15px;
            color: #333333;
        }
        .info-box {
            background-color: #f8f9fa;
            border-left: 4px solid #ffd700;
            padding: 20px;
            border-radius: 8px;
            margin: 20px 0;
        }
        .email-footer {
            background-color: #1a1a1a;
            color: #ffffff;
            padding: 30px 20px;
            text-align: center;
            border-top: 1px solid #e9ecef;
        }
        .footer-content {
            color: #cccccc;
            font-size: 12px;
            line-height: 1.5;
        }
        .footer-content p {
            margin: 5px 0;
            color: #cccccc;
        }
        .btn {
            display: inline-block;
            padding: 14px 28px;
            background: linear-gradient(135deg, #ffd700 0%, #ffed4e 100%);
            color: #1a1a1a;
            text-decoration: none;
            border-radius: 8px;
            margin: 15px 0;
            font-weight: 600;
            font-size: 14px;
            transition: all 0.3s ease;
            box-shadow: 0 2px 8px rgba(255, 215, 0, 0.3);
        }
        .btn:hover {
            background: linear-gradient(135deg, #ffed4e 0%, #ffd700 100%);
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(255, 215, 0, 0.4);
        }
        .highlight {
            color: #ffd700;
            font-weight: 600;
        }
        .divider {
            height: 1px;
            background: linear-gradient(90deg, transparent, #ffd700, transparent);
            margin: 25px 0;
        }
        @media only screen and (max-width: 600px) {
            .email-container {
                margin: 0;
                border-radius: 0;
            }
            .email-content {
                padding: 30px 20px;
            }
            .email-header {
                padding: 30px 15px 25px;
            }
            .logo {
                max-width: 150px;
            }
        }
    </style>
</head>";
    }

    private string GenerateEmailFooter(Dictionary<string, object> context)
    {
        var business = context.GetValueOrDefault("business", _businessInfo.Business).ToString();
        var year = context.GetValueOrDefault("year", DateTime.Now.Year).ToString();
        var phone = context.GetValueOrDefault("phone", _businessInfo.Phone).ToString();
        var address = context.GetValueOrDefault("address", _businessInfo.Address).ToString();
        var contact = context.GetValueOrDefault("contact", _businessInfo.Contact).ToString();

        return $@"
        <div class=""email-footer"">
            <div class=""footer-content"">
                <p>© {year} <span class=""highlight"">{business}</span>. Todos los derechos reservados.</p>
                <p>{address} | {phone} | {contact}</p>
            </div>
        </div>";
    }

    private string ReplaceTemplateVariables(string content, Dictionary<string, object> context)
    {
        var processedContent = content;

        foreach (var variable in context)
        {
            var placeholder = $"{{{{{variable.Key}}}}}";
            var value = variable.Value?.ToString() ?? "";
            processedContent = processedContent.Replace(placeholder, value);
        }

        return processedContent;
    }
}
