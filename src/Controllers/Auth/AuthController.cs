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
    IEmailService emailService,
    ILogger<AuthController> logger
) : ControllerBase
{
    private readonly CorsConfiguration _corsConfig = corsConfig.Value;

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
