using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using GestionHogar.Configuration;
using GestionHogar.Model;
using GestionHogar.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GestionHogar.Controllers;

[ApiController]
[Route("/api/[controller]")]
public class AuthController(
    JwtService jwt,
    UserManager<User> userManager,
    IOptions<CorsConfiguration> corsConfig,
    IOptions<BusinessInfo> businessInfo,
    IEmailService emailService,
    ILogger<AuthController> logger
) : ControllerBase
{
    private readonly CorsConfiguration _corsConfig = corsConfig.Value;
    private readonly BusinessInfo _businessInfo = businessInfo.Value;

    private void SetAuthCookies(string accessToken, string refreshToken)
    {
        // Access token cookie - no HttpOnly para que el frontend pueda leerlo
        var accessCookieOptions = new CookieOptions { HttpOnly = false };

        // Refresh token cookie - HttpOnly para seguridad
        var refreshCookieOptions = new CookieOptions { HttpOnly = true };

        logger.LogInformation("Cookie domain: {CookieDomain}", _corsConfig.CookieDomain);
#if DEBUG
        accessCookieOptions.SameSite = SameSiteMode.Lax;
        accessCookieOptions.Secure = false;
        refreshCookieOptions.SameSite = SameSiteMode.Lax;
        refreshCookieOptions.Secure = false;
        // For localhost development, don't set domain to allow cross-port access
        if (!string.IsNullOrEmpty(_corsConfig.CookieDomain))
        {
            accessCookieOptions.Domain = _corsConfig.CookieDomain;
            refreshCookieOptions.Domain = _corsConfig.CookieDomain;
        }
#else
        accessCookieOptions.SameSite = SameSiteMode.None;
        accessCookieOptions.Secure = true;
        refreshCookieOptions.SameSite = SameSiteMode.None;
        refreshCookieOptions.Secure = true;
        // In production, set the domain for cross-subdomain access
        if (!string.IsNullOrEmpty(_corsConfig.CookieDomain))
        {
            accessCookieOptions.Domain = _corsConfig.CookieDomain;
            refreshCookieOptions.Domain = _corsConfig.CookieDomain;
        }
#endif

        // Set access token cookie
        accessCookieOptions.Expires = DateTime.UtcNow.AddSeconds(_corsConfig.ExpirationSeconds);
        Response.Cookies.Append(_corsConfig.CookieName, accessToken, accessCookieOptions);

        // Set refresh token cookie
        refreshCookieOptions.Expires = DateTime.UtcNow.AddSeconds(
            _corsConfig.RefreshExpirationSeconds
        );
        Response.Cookies.Append(
            $"{_corsConfig.CookieName}_refresh",
            refreshToken,
            refreshCookieOptions
        );
    }

    [EndpointSummary("Login")]
    [EndpointDescription(
        "Log in to the system. Returns 2 JWT tokens, access_token and refresh_token. access_token is to be used in Authorization Bearer."
    )]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return NotFound("Usuario no encontrado");

        // Validar que el usuario esté activo
        if (!user.IsActive)
            return BadRequest(
                "Usuario inactivo. Contacte al administrador para reactivar su cuenta."
            );

        var isValid = await userManager.CheckPasswordAsync(user, request.Password);
        if (!isValid)
            return BadRequest("Credenciales incorrectas");

        var userRoles = await userManager.GetRolesAsync(user);

        var (token, accessExpiration) = jwt.GenerateToken(
            userId: user.Id.ToString(),
            username: request.Email,
            roles: userRoles,
            user.SecurityStamp ?? ""
        );
        var (refreshToken, refreshExpiration) = jwt.GenerateRefreshToken(
            user.Id.ToString(),
            request.Email
        );

        // Set authentication cookies
        SetAuthCookies(token, refreshToken);

        // Enviar notificación de seguridad por email
        try
        {
            var userAgent = Request.Headers["User-Agent"].FirstOrDefault();
            var ipAddress = GetRealIPAddress();
            var deviceInfo = GetDeviceInfo(userAgent);
            var location = GetLocationByIP(ipAddress);
            var loginTime = DateTime.Now;

            var securityNotificationContent = GenerateSecurityNotificationEmailContent(
                user.Name ?? "Usuario",
                request.Email,
                deviceInfo,
                location,
                ipAddress ?? "IP no disponible",
                loginTime
            );

            var emailRequest = new EmailRequest
            {
                To = request.Email,
                Subject = $"Nuevo inicio de sesión en {_businessInfo.Business ?? "Gestion Hogar"}",
                Content = securityNotificationContent,
            };

            var emailSent = await emailService.SendEmailAsync(emailRequest);

            if (emailSent)
            {
                logger.LogInformation(
                    "✅ EMAIL ENVIADO: Notificación de seguridad enviada exitosamente a {Email}",
                    request.Email
                );
            }
            else
            {
                logger.LogWarning(
                    "❌ EMAIL FALLÓ: No se pudo enviar notificación de seguridad a {Email}",
                    request.Email
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error al enviar notificación de seguridad a {Email}",
                request.Email
            );
            // No fallamos el login si el email falla
        }

        return new LoginResponse(
            AccessToken: token,
            RefreshToken: refreshToken,
            AccessExpiresIn: accessExpiration,
            RefreshExpiresIn: refreshExpiration
        );
    }

    [EndpointSummary("Validate token")]
    [EndpointDescription("Validates the current access token and returns user information.")]
    [HttpPost("validate")]
    public async Task<ActionResult<UserInfo>> ValidateToken()
    {
        try
        {
            // Obtener el token del header Authorization
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Token no proporcionado");
            }

            var token = authHeader.Substring("Bearer ".Length);

            // Validar el token usando el JwtService
            var userId = jwt.ValidateToken(token);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Token inválido");
            }

            // Obtener usuario
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Unauthorized("Usuario no encontrado");
            }

            // Verificar que el usuario esté activo
            if (!user.IsActive)
            {
                return Unauthorized("Usuario inactivo");
            }

            var userRoles = await userManager.GetRolesAsync(user);

            return Ok(
                new UserInfo
                {
                    Id = user.Id.ToString(),
                    Name = user.Name ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    Roles = userRoles.ToArray(),
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validando token");
            return Unauthorized("Token inválido");
        }
    }

    [EndpointSummary("Refresh session")]
    [EndpointDescription("Refreshes the session, returning 2 new JWT tokens.")]
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> refresh()
    {
        try
        {
            // Obtener refresh token de las cookies
            var refreshToken = Request.Cookies[$"{_corsConfig.CookieName}_refresh"];
            if (string.IsNullOrEmpty(refreshToken))
            {
                return Unauthorized("Refresh token no encontrado en cookies");
            }

            // Validar refresh token
            var userId = jwt.ValidateRefreshToken(refreshToken);
            if (userId == null)
            {
                return Unauthorized("Refresh token inválido");
            }

            // Obtener usuario
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Unauthorized("Usuario no encontrado");
            }

            // Verificar que el usuario esté activo
            if (!user.IsActive)
            {
                return Unauthorized("Usuario inactivo");
            }

            // Obtener roles actuales del usuario
            var userRoles = await userManager.GetRolesAsync(user);

            var (token, accessExpiration) = jwt.GenerateToken(
                userId: user.Id.ToString(),
                username: user.Email!,
                roles: userRoles,
                user.SecurityStamp ?? ""
            );
            var (newRefreshToken, refreshExpiration) = jwt.GenerateRefreshToken(
                user.Id.ToString(),
                user.Email!
            );

            // Set authentication cookies
            SetAuthCookies(token, newRefreshToken);

            return new LoginResponse(
                AccessToken: token,
                RefreshToken: newRefreshToken,
                AccessExpiresIn: accessExpiration,
                RefreshExpiresIn: refreshExpiration
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error en refresh token");
            return Unauthorized("Error al renovar sesión");
        }
    }

    [EndpointSummary("Logout")]
    [EndpointDescription("Logs out the user by clearing authentication cookies.")]
    [HttpPost("logout")]
    public ActionResult logout()
    {
        // Clear authentication cookies with proper options for both local and production
        var accessCookieOptions = new CookieOptions { HttpOnly = false };
        var refreshCookieOptions = new CookieOptions { HttpOnly = true };

#if DEBUG
        accessCookieOptions.SameSite = SameSiteMode.Lax;
        accessCookieOptions.Secure = false;
        refreshCookieOptions.SameSite = SameSiteMode.Lax;
        refreshCookieOptions.Secure = false;
        // For localhost development, don't set domain to allow cross-port access
        if (!string.IsNullOrEmpty(_corsConfig.CookieDomain))
        {
            accessCookieOptions.Domain = _corsConfig.CookieDomain;
            refreshCookieOptions.Domain = _corsConfig.CookieDomain;
        }
#else
        accessCookieOptions.SameSite = SameSiteMode.None;
        accessCookieOptions.Secure = true;
        refreshCookieOptions.SameSite = SameSiteMode.None;
        refreshCookieOptions.Secure = true;
        // In production, set the domain for cross-subdomain access
        if (!string.IsNullOrEmpty(_corsConfig.CookieDomain))
        {
            accessCookieOptions.Domain = _corsConfig.CookieDomain;
            refreshCookieOptions.Domain = _corsConfig.CookieDomain;
        }
#endif

        // Set expiration to past date to effectively delete the cookies
        accessCookieOptions.Expires = DateTime.UtcNow.AddDays(-1);
        refreshCookieOptions.Expires = DateTime.UtcNow.AddDays(-1);

        // Clear authentication cookies
        Response.Cookies.Append(_corsConfig.CookieName, "", accessCookieOptions);
        Response.Cookies.Append($"{_corsConfig.CookieName}_refresh", "", refreshCookieOptions);

        return Ok(new { message = "Logged out successfully" });
    }

    /// <summary>
    /// Genera el contenido HTML del email de notificación de seguridad
    /// </summary>
    private string GenerateSecurityNotificationEmailContent(
        string userName,
        string email,
        string deviceInfo,
        string location,
        string ipAddress,
        DateTime loginTime
    )
    {
        var businessName = _businessInfo.Business ?? "Gestion Hogar";
        var businessUrl = _businessInfo.Url ?? "https://gestionhogar.com";

        return $@"
        <h1 style=""color: #1a1a1a; font-weight: 700; font-size: 28px; margin-bottom: 25px; text-align: center;"">
            Nuevo inicio de sesión en {businessName}
        </h1>
        
        <p style=""font-size: 16px; color: #1a1a1a; margin-bottom: 20px;"">
            Estimado(a) <span class=""highlight"">{userName}</span>,
        </p>
        
        <div class=""info-box"">
            <p style=""font-size: 15px; color: #1a1a1a; margin-bottom: 15px;"">
                Identificamos un nuevo inicio de sesión en <span class=""highlight"">{businessName}</span>. ¿Accediste a tu cuenta? Estos son los detalles:
            </p>
        </div>
        
        <div class=""info-box"">
            <h3 style=""color: #1a1a1a; font-weight: 600; margin-bottom: 15px;"">
                Detalles del inicio de sesión:
            </h3>
            <ul style=""color: #333333; font-size: 14px; margin: 0; padding-left: 20px;"">
                <li style=""margin-bottom: 8px;""><strong>Tipo de dispositivo:</strong> <span class=""highlight"">{deviceInfo}</span></li>
                <li style=""margin-bottom: 8px;""><strong>Ubicación:</strong> <span class=""highlight"">{location}</span></li>
                <li style=""margin-bottom: 8px;""><strong>Dirección IP:</strong> <span class=""highlight"">{ipAddress}</span></li>
                <li style=""margin-bottom: 8px;""><strong>Fecha y hora:</strong> <span class=""highlight"">{loginTime:dd/MM/yyyy HH:mm}</span></li>
            </ul>
        </div>
        
        <div class=""info-box"">
            <h3 style=""color: #1a1a1a; font-weight: 600; margin-bottom: 15px;"">
                ¿Qué hacer?
            </h3>
            <ul style=""color: #333333; font-size: 14px; margin: 0; padding-left: 20px;"">
                <li style=""margin-bottom: 8px;"">Si te resulta conocido, ignora este mensaje.</li>
                <li style=""margin-bottom: 8px;"">Te recomendamos verificar la actividad de tus dispositivos.</li>
                <li style=""margin-bottom: 8px;"">¿No accediste a tu cuenta? Te sugerimos restablecer la contraseña.</li>
            </ul>
        </div>
        
        <div class=""divider""></div>
        
        <p style=""font-size: 14px; color: #666666; text-align: center; margin-top: 25px;"">
            Siempre estamos a tu disposición. En caso de preguntas, hay más información en el Centro de ayuda.
        </p>
        
        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{businessUrl}"" class=""btn"">Acceder a la Plataforma</a>
        </div>";
    }

    /// <summary>
    /// Obtiene información del dispositivo desde el User-Agent
    /// </summary>
    private string GetDeviceInfo(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return "Dispositivo desconocido";

        var ua = userAgent.ToLower();

        if (ua.Contains("windows"))
            return "Windows PC";
        if (ua.Contains("macintosh") || ua.Contains("mac os"))
            return "Mac";
        if (ua.Contains("linux"))
            return "Linux PC";
        if (ua.Contains("android"))
            return "Android Device";
        if (ua.Contains("iphone") || ua.Contains("ipad"))
            return "iOS Device";
        if (ua.Contains("playstation"))
            return "PlayStation Game Console";
        if (ua.Contains("xbox"))
            return "Xbox Game Console";
        if (ua.Contains("nintendo"))
            return "Nintendo Game Console";

        return "Dispositivo desconocido";
    }

    /// <summary>
    /// Obtiene la IP real del cliente, considerando proxies y load balancers
    /// </summary>
    private string GetRealIPAddress()
    {
        // Verificar headers de proxies/load balancers
        var xForwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        var xRealIP = Request.Headers["X-Real-IP"].FirstOrDefault();
        var cfConnectingIP = Request.Headers["CF-Connecting-IP"].FirstOrDefault(); // Cloudflare

        if (!string.IsNullOrEmpty(cfConnectingIP))
            return cfConnectingIP;

        if (!string.IsNullOrEmpty(xRealIP))
            return xRealIP;

        if (!string.IsNullOrEmpty(xForwardedFor))
        {
            // X-Forwarded-For puede contener múltiples IPs separadas por comas
            var ips = xForwardedFor.Split(',');
            return ips[0].Trim();
        }

        // Fallback a la IP de conexión directa
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP no disponible";
    }

    /// <summary>
    /// Obtiene ubicación aproximada por IP (simplificado)
    /// </summary>
    private string GetLocationByIP(string? ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress) || ipAddress == "::1" || ipAddress == "127.0.0.1")
            return "Desarrollo local";

        // Para desarrollo local, retornar ubicación genérica
        // En producción, podrías usar un servicio de geolocalización como:
        // - ipapi.co
        // - ip-api.com
        // - ipgeolocation.io
        return "Ubicación no disponible";
    }
}

public class LoginRequest
{
    [MinLength(3)]
    [MaxLength(100)]
    [DefaultValue("admin@admin.com")]
    public required string Email { get; set; }

    [MinLength(8)]
    [MaxLength(100)]
    [DefaultValue("Acide2025/1")]
    public required string Password { get; set; }
}

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    int AccessExpiresIn,
    int RefreshExpiresIn
);

public class UserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string[] Roles { get; set; } = Array.Empty<string>();
}
