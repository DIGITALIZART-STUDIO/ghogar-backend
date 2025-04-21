using System.IdentityModel.Tokens.Jwt;
using GestionHogar.Model;
using Microsoft.AspNetCore.Identity;

namespace GestionHogar.Controllers;

public class SecurityStampValidator
{
    private readonly RequestDelegate _next;

    public SecurityStampValidator(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, UserManager<User> userManager)
    {
        if (context.User.Identity?.IsAuthenticated ?? false)
        {
            var userId = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var stampInToken = context.User.FindFirst("securityStamp")?.Value;

            if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(stampInToken))
            {
                var user = await userManager.FindByIdAsync(userId);
                if (user == null || user.SecurityStamp != stampInToken)
                {
                    // Token is invalid because security stamp doesn't match
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync(
                        "Authentication failed: Security stamp mismatch"
                    );
                    return;
                }
            }
        }

        await _next(context);
    }
}

// Extension method to make it easier to use in Startup.cs
public static class SecurityStampValidatorExtensions
{
    public static IApplicationBuilder UseSecurityStampValidator(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityStampValidator>();
    }
}
