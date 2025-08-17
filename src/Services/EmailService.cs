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

    public EmailService(
        IOptions<EmailConfiguration> emailConfig,
        IOptions<BusinessInfo> businessInfo,
        ILogger<EmailService> logger
    )
    {
        _emailConfig = emailConfig.Value;
        _businessInfo = businessInfo.Value;
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

    private async Task<string> GenerateEmailTemplateAsync(
        string content,
        Dictionary<string, object> context
    )
    {
        var header = GenerateEmailHeader();
        var footer = GenerateEmailFooter(context);

        // Reemplazar variables en el contenido
        var processedContent = ReplaceTemplateVariables(content, context);

        return $@"
<!DOCTYPE html>
<html lang=""es"">
{header}
<body>
    <div style=""max-width: 600px; margin: 0 auto; background-color: #ffffff; font-family: 'Arial', sans-serif;"">
        {processedContent}
    </div>
    {footer}
</body>
</html>";
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
        body {
            font-family: 'Arial', sans-serif;
            font-optical-sizing: auto;
            background-color: #f9f9ff;
            margin: 0;
            padding: 0;
            -webkit-font-smoothing: antialiased;
            color: #0a0a0a;
        }
        .email-container {
            max-width: 600px;
            margin: 0 auto;
            background-color: #ffffff;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            overflow: hidden;
        }
        .email-header {
            background: linear-gradient(135deg, #034a5b 0%, #05668b 100%);
            color: white;
            padding: 30px 20px;
            text-align: center;
        }
        .email-content {
            padding: 30px 20px;
            line-height: 1.6;
        }
        .email-footer {
            background-color: #f8f9fa;
            padding: 20px;
            text-align: center;
            border-top: 1px solid #e9ecef;
        }
        .btn {
            display: inline-block;
            padding: 12px 24px;
            background-color: #034a5b;
            color: white;
            text-decoration: none;
            border-radius: 5px;
            margin: 10px 0;
        }
        .btn:hover {
            background-color: #05668b;
        }
        @media only screen and (max-width: 600px) {
            .email-container {
                margin: 0;
                border-radius: 0;
            }
            .email-content {
                padding: 20px 15px;
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
            <div style=""color: #034a5b; font-size: 12px;"">
                <p>© {year} {business}. Todos los derechos reservados.</p>
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
