using GestionHogar.Services;

namespace GestionHogar.Services;

public interface IEmailTemplateService
{
    string GetWelcomeTemplate(string name, string email, string password, string webAdminUrl);
    string GetPasswordResetTemplate(
        string name,
        string email,
        string newPassword,
        string webAdminUrl
    );
    string GetQuotationTemplate(
        string clientName,
        string quotationNumber,
        decimal amount,
        string projectName
    );
    string GetReservationTemplate(
        string clientName,
        string lotNumber,
        string projectName,
        decimal amount
    );
    string GetPaymentReminderTemplate(
        string clientName,
        string paymentNumber,
        decimal amount,
        DateTime dueDate
    );
    string GetCustomTemplate(string templateName, Dictionary<string, object> context);
}

public class EmailTemplateService : IEmailTemplateService
{
    public string GetWelcomeTemplate(string name, string email, string password, string webAdminUrl)
    {
        return $@"
    <h1>¡Bienvenido a <span class=""highlight"">{{business}}</span>!</h1>
    
    <h2>¡Hola {name}!</h2>
    
    <p>Te damos la bienvenida a <strong class=""highlight"">{{business}}</strong>. Tu cuenta ha sido creada exitosamente.</p>
    
    <div class=""info-box"">
        <h3>Información de tu cuenta:</h3>
        <p><strong>Email:</strong> {email}</p>
        <p><strong>Contraseña temporal:</strong> {password}</p>
        <p><strong>URL del administrador:</strong> <a href=""{webAdminUrl}"">{webAdminUrl}</a></p>
    </div>
    
    <p><strong>Importante:</strong> Por seguridad, te recomendamos cambiar tu contraseña después del primer inicio de sesión.</p>
    
    <div style=""text-align: center; margin: 30px 0;"">
        <a href=""{webAdminUrl}"" class=""btn"">Acceder al Panel de Administración</a>
    </div>
    
    <div class=""divider""></div>
    
    <p>Si tienes alguna pregunta, no dudes en contactarnos.</p>
    
    <p>Saludos,<br><strong>El equipo de {{business}}</strong></p>";
    }

    public string GetPasswordResetTemplate(
        string name,
        string email,
        string newPassword,
        string webAdminUrl
    )
    {
        return $@"
    <h1>Hola de nuevo, <span class=""highlight"">{name}</span>!</h1>
    
    <p>Hemos recibido una solicitud para restablecer tu contraseña en <strong class=""highlight"">{{business}}</strong>.</p>
    
    <div class=""info-box"">
        <h3>Tu nueva contraseña:</h3>
        <p><strong>{newPassword}</strong></p>
    </div>
    
    <p><strong>Importante:</strong> Por seguridad, te recomendamos cambiar esta contraseña después del inicio de sesión.</p>
    
    <div style=""text-align: center; margin: 30px 0;"">
        <a href=""{webAdminUrl}"" class=""btn"">Iniciar Sesión</a>
    </div>
    
    <div class=""divider""></div>
    
    <p>Si no solicitaste este cambio, por favor contacta con nuestro equipo de soporte inmediatamente.</p>
    
    <p>Saludos,<br><strong>El equipo de {{business}}</strong></p>";
    }

    public string GetQuotationTemplate(
        string clientName,
        string quotationNumber,
        decimal amount,
        string projectName
    )
    {
        return $@"
    <h1>¡Hola <span class=""highlight"">{clientName}</span>!</h1>
    
    <p>Hemos preparado una cotización especial para ti en <strong class=""highlight"">{{business}}</strong>.</p>
    
    <div class=""info-box"">
        <h3>Detalles de la Cotización:</h3>
        <p><strong>Número de Cotización:</strong> {quotationNumber}</p>
        <p><strong>Proyecto:</strong> {projectName}</p>
        <p><strong>Monto Total:</strong> <span class=""highlight"">S/ {amount:N2}</span></p>
    </div>
    
    <p>Esta cotización es válida por 30 días. Para más información o para proceder con la reserva, contáctanos.</p>
    
    <div style=""text-align: center; margin: 30px 0;"">
        <a href=""{{url}}"" class=""btn"">Ver Detalles</a>
    </div>
    
    <div class=""divider""></div>
    
    <p>¡Gracias por confiar en <strong class=""highlight"">{{business}}</strong>!</p>
    
    <p>Saludos,<br><strong>El equipo de {{business}}</strong></p>";
    }

    public string GetReservationTemplate(
        string clientName,
        string lotNumber,
        string projectName,
        decimal amount
    )
    {
        return $@"
    <h1>¡Felicidades <span class=""highlight"">{clientName}</span>!</h1>
    
    <p>Tu reserva ha sido confirmada exitosamente en <strong class=""highlight"">{{business}}</strong>.</p>
    
    <div class=""info-box"">
        <h3>Detalles de tu Reserva:</h3>
        <p><strong>Lote:</strong> {lotNumber}</p>
        <p><strong>Proyecto:</strong> {projectName}</p>
        <p><strong>Monto de Reserva:</strong> <span class=""highlight"">S/ {amount:N2}</span></p>
    </div>
    
    <p>En los próximos días recibirás información adicional sobre los siguientes pasos del proceso.</p>
    
    <div style=""text-align: center; margin: 30px 0;"">
        <a href=""{{url}}"" class=""btn"">Ver Mi Reserva</a>
    </div>
    
    <div class=""divider""></div>
    
    <p>¡Bienvenido a la familia <strong class=""highlight"">{{business}}</strong>!</p>
    
    <p>Saludos,<br><strong>El equipo de {{business}}</strong></p>";
    }

    public string GetPaymentReminderTemplate(
        string clientName,
        string paymentNumber,
        decimal amount,
        DateTime dueDate
    )
    {
        return $@"
    <h1>Recordatorio de Pago - <span class=""highlight"">{clientName}</span></h1>
    
    <p>Este es un recordatorio amigable sobre tu próximo pago en <strong class=""highlight"">{{business}}</strong>.</p>
    
    <div class=""info-box"">
        <h3>Detalles del Pago:</h3>
        <p><strong>Número de Pago:</strong> {paymentNumber}</p>
        <p><strong>Monto:</strong> <span class=""highlight"">S/ {amount:N2}</span></p>
        <p><strong>Fecha de Vencimiento:</strong> {dueDate:dd/MM/yyyy}</p>
    </div>
    
    <p>Para evitar cargos adicionales, te recomendamos realizar el pago antes de la fecha de vencimiento.</p>
    
    <div style=""text-align: center; margin: 30px 0;"">
        <a href=""{{url}}"" class=""btn"">Realizar Pago</a>
    </div>
    
    <div class=""divider""></div>
    
    <p>Si ya realizaste el pago, puedes ignorar este mensaje.</p>
    
    <p>Saludos,<br><strong>El equipo de {{business}}</strong></p>";
    }

    public string GetCustomTemplate(string templateName, Dictionary<string, object> context)
    {
        // Aquí puedes agregar más templates personalizados según necesites
        return templateName.ToLower() switch
        {
            "welcome" => GetWelcomeTemplate(
                context.GetValueOrDefault("name", "").ToString()!,
                context.GetValueOrDefault("email", "").ToString()!,
                context.GetValueOrDefault("password", "").ToString()!,
                context.GetValueOrDefault("webAdminUrl", "").ToString()!
            ),
            "password-reset" => GetPasswordResetTemplate(
                context.GetValueOrDefault("name", "").ToString()!,
                context.GetValueOrDefault("email", "").ToString()!,
                context.GetValueOrDefault("newPassword", "").ToString()!,
                context.GetValueOrDefault("webAdminUrl", "").ToString()!
            ),
            "quotation" => GetQuotationTemplate(
                context.GetValueOrDefault("clientName", "").ToString()!,
                context.GetValueOrDefault("quotationNumber", "").ToString()!,
                Convert.ToDecimal(context.GetValueOrDefault("amount", 0)),
                context.GetValueOrDefault("projectName", "").ToString()!
            ),
            "reservation" => GetReservationTemplate(
                context.GetValueOrDefault("clientName", "").ToString()!,
                context.GetValueOrDefault("lotNumber", "").ToString()!,
                context.GetValueOrDefault("projectName", "").ToString()!,
                Convert.ToDecimal(context.GetValueOrDefault("amount", 0))
            ),
            "payment-reminder" => GetPaymentReminderTemplate(
                context.GetValueOrDefault("clientName", "").ToString()!,
                context.GetValueOrDefault("paymentNumber", "").ToString()!,
                Convert.ToDecimal(context.GetValueOrDefault("amount", 0)),
                Convert.ToDateTime(context.GetValueOrDefault("dueDate", DateTime.Now))
            ),
            _ => throw new ArgumentException($"Template '{templateName}' no encontrado."),
        };
    }
}
