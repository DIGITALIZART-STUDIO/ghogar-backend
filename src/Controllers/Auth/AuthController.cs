using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using GestionHogar.Configuration;
using GestionHogar.Model;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GestionHogar.Controllers;

[ApiController]
[Route("/api/[controller]")]
public class AuthController(
    JwtService jwt,
    UserManager<User> userManager,
    IOptions<CorsConfiguration> corsConfig
) : ControllerBase
{
    private readonly CorsConfiguration _corsConfig = corsConfig.Value;

    private void SetAuthCookies(string accessToken, string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = false, // Set to true in production with HTTPS
        };

#if DEBUG
        cookieOptions.SameSite = SameSiteMode.Lax;
        cookieOptions.Secure = false;
        // For localhost development, don't set domain to allow cross-port access
        if (!string.IsNullOrEmpty(_corsConfig.CookieDomain))
        {
            cookieOptions.Domain = _corsConfig.CookieDomain;
        }
#else
        cookieOptions.SameSite = SameSiteMode.None;
        cookieOptions.Secure = true;
        // In production, set the domain for cross-subdomain access
        if (!string.IsNullOrEmpty(_corsConfig.CookieDomain))
        {
            cookieOptions.Domain = _corsConfig.CookieDomain;
        }
#endif

        // Set access token cookie
        cookieOptions.Expires = DateTime.UtcNow.AddSeconds(_corsConfig.ExpirationSeconds);
        Response.Cookies.Append(_corsConfig.CookieName, accessToken, cookieOptions);

        // Set refresh token cookie
        cookieOptions.Expires = DateTime.UtcNow.AddSeconds(_corsConfig.RefreshExpirationSeconds);
        Response.Cookies.Append($"{_corsConfig.CookieName}_refresh", refreshToken, cookieOptions);
    }

    [EndpointSummary("Login")]
    [EndpointDescription(
        "Log in to the system. Returns 2 JWT tokens, access_token and refresh_token. access_token is to be used in Authorization Bearer."
    )]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> login([FromBody] LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return Unauthorized("Credenciales incorrectos");

        var isValid = await userManager.CheckPasswordAsync(user, request.Password);
        if (!isValid)
            return Unauthorized("Credenciales incorrectos");

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

    [EndpointSummary("Refresh session")]
    [EndpointDescription("Refreshes the session, returning 2 new JWT tokens.")]
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> refresh([FromBody] RefreshRequest req)
    {
        // validate refresh token
        var userId = jwt.ValidateRefreshToken(req.RefreshToken);
        if (userId == null)
        {
            return Unauthorized("Credencial invalido, inicie sesión de nuevo");
        }

        // get user from userid
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Unauthorized("Credencial invalido, inicie sesión de nuevo");
        }

        var (token, accessExpiration) = jwt.GenerateToken(
            userId: user.Id.ToString(),
            username: user.Email!,
            roles: new[] { "User" },
            user.SecurityStamp ?? ""
        );
        var (refreshToken, refreshExpiration) = jwt.GenerateRefreshToken(
            user.Id.ToString(),
            user.Email!
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

    [EndpointSummary("Logout")]
    [EndpointDescription("Logs out the user by clearing authentication cookies.")]
    [HttpPost("logout")]
    public ActionResult logout()
    {
        // Clear authentication cookies
        Response.Cookies.Delete(_corsConfig.CookieName);
        Response.Cookies.Delete($"{_corsConfig.CookieName}_refresh");

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

public class RefreshRequest
{
    [MinLength(1)]
    public required string RefreshToken { get; set; }
}

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    int AccessExpiresIn,
    int RefreshExpiresIn
);
