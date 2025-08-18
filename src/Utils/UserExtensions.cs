using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GestionHogar.Model;

namespace GestionHogar.Utils;

public static class UserExtensions
{
    /// <summary>
    /// Obtiene el ID del usuario actual desde los claims del JWT
    /// </summary>
    /// <param name="user">ClaimsPrincipal del usuario</param>
    /// <returns>Guid del usuario o null si no se puede obtener</returns>
    public static Guid? GetCurrentUserId(this ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return null;

        // Intentar obtener el ID desde diferentes claims posibles
        var userId =
            user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? user.FindFirstValue("id");

        if (string.IsNullOrEmpty(userId))
            return null;

        return Guid.TryParse(userId, out var guid) ? guid : null;
    }

    /// <summary>
    /// Obtiene el ID del usuario actual desde los claims del JWT
    /// Lanza una excepción si no se puede obtener
    /// </summary>
    /// <param name="user">ClaimsPrincipal del usuario</param>
    /// <returns>Guid del usuario</returns>
    /// <exception cref="UnauthorizedAccessException">Si no se puede obtener el ID del usuario</exception>
    public static Guid GetCurrentUserIdOrThrow(this ClaimsPrincipal user)
    {
        var userId = user.GetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new UnauthorizedAccessException("No se pudo identificar al usuario actual");
        }
        return userId.Value;
    }

    /// <summary>
    /// Obtiene el email del usuario actual desde los claims del JWT
    /// </summary>
    /// <param name="user">ClaimsPrincipal del usuario</param>
    /// <returns>Email del usuario o null si no se puede obtener</returns>
    public static string? GetCurrentUserEmail(this ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return null;

        return user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue("email")
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Name);
    }

    /// <summary>
    /// Obtiene el nombre del usuario actual desde los claims del JWT
    /// </summary>
    /// <param name="user">ClaimsPrincipal del usuario</param>
    /// <returns>Nombre del usuario o null si no se puede obtener</returns>
    public static string? GetCurrentUserName(this ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return null;

        return user.FindFirstValue(ClaimTypes.Name)
            ?? user.FindFirstValue("name")
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Name);
    }

    /// <summary>
    /// Obtiene los roles del usuario actual desde los claims del JWT
    /// </summary>
    /// <param name="user">ClaimsPrincipal del usuario</param>
    /// <returns>Lista de roles del usuario</returns>
    public static IEnumerable<string> GetCurrentUserRoles(this ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return Enumerable.Empty<string>();

        return user.FindAll(ClaimTypes.Role).Select(c => c.Value);
    }

    /// <summary>
    /// Verifica si el usuario actual tiene un rol específico
    /// </summary>
    /// <param name="user">ClaimsPrincipal del usuario</param>
    /// <param name="role">Rol a verificar</param>
    /// <returns>True si el usuario tiene el rol, false en caso contrario</returns>
    public static bool HasRole(this ClaimsPrincipal user, string role)
    {
        return user.GetCurrentUserRoles().Contains(role);
    }

    /// <summary>
    /// Verifica si el usuario actual tiene alguno de los roles especificados
    /// </summary>
    /// <param name="user">ClaimsPrincipal del usuario</param>
    /// <param name="roles">Roles a verificar</param>
    /// <returns>True si el usuario tiene al menos uno de los roles, false en caso contrario</returns>
    public static bool HasAnyRole(this ClaimsPrincipal user, params string[] roles)
    {
        var userRoles = user.GetCurrentUserRoles().ToHashSet();
        return roles.Any(role => userRoles.Contains(role));
    }
}
