using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GestionHogar.Utils;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuthorizeCurrentUserAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _requiredRoles;

    public AuthorizeCurrentUserAttribute(params string[] requiredRoles)
    {
        _requiredRoles = requiredRoles;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        // Verificar si el usuario está autenticado
        if (!user.Identity?.IsAuthenticated == true)
        {
            context.Result = new UnauthorizedObjectResult(
                new
                {
                    error = "Unauthorized",
                    message = "Usuario no autenticado",
                    timestamp = DateTime.UtcNow,
                }
            );
            return;
        }

        // Verificar si se puede obtener el ID del usuario
        var userId = user.GetCurrentUserId();
        if (!userId.HasValue)
        {
            context.Result = new UnauthorizedObjectResult(
                new
                {
                    error = "Unauthorized",
                    message = "No se pudo identificar al usuario actual",
                    timestamp = DateTime.UtcNow,
                }
            );
            return;
        }

        // Verificar roles si se especificaron
        if (_requiredRoles.Length > 0)
        {
            var userRoles = user.GetCurrentUserRoles().ToHashSet();
            var hasRequiredRole = _requiredRoles.Any(role => userRoles.Contains(role));

            if (!hasRequiredRole)
            {
                context.Result = new ForbidResult();
                return;
            }
        }
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireRoleAttribute : Attribute, IAuthorizationFilter
{
    private readonly string _requiredRole;

    public RequireRoleAttribute(string requiredRole)
    {
        _requiredRole = requiredRole;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (!user.Identity?.IsAuthenticated == true)
        {
            context.Result = new UnauthorizedObjectResult(
                new
                {
                    error = "Unauthorized",
                    message = "Usuario no autenticado",
                    timestamp = DateTime.UtcNow,
                }
            );
            return;
        }

        if (!user.HasRole(_requiredRole))
        {
            context.Result = new ForbidResult();
            return;
        }
    }
}
