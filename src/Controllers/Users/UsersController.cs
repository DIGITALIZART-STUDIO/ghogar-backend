using System.Security.Claims;
using GestionHogar.Configuration;
using GestionHogar.Model;
using GestionHogar.Services;
using GestionHogar.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GestionHogar.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController(
    DatabaseContext db,
    UserManager<User> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IEmailService emailService,
    IOptions<BusinessInfo> businessInfo,
    IUserHigherRankService userHigherRankService,
    ILogger<UsersController> logger
) : ControllerBase
{
    [EndpointSummary("Get current user")]
    [EndpointDescription("Gets information about the currently logged-in user")]
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<UserGetDTO>> CurrentUser()
    {
        try
        {
            var userId = User.GetCurrentUserIdOrThrow();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return Unauthorized("Usuario no encontrado");

            var userRoles = await userManager.GetRolesAsync(user);
            if (userRoles == null)
                return Unauthorized("No se pudieron obtener los roles del usuario");

            return Ok(new UserGetDTO() { User = user, Roles = userRoles });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized("No se pudo identificar al usuario actual");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al obtener información del usuario actual");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [EndpointSummary("Get all users")]
    [EndpointDescription("Gets information about all users")]
    [Authorize]
    [HttpGet("all")]
    public async Task<ActionResult<PaginatedResponseV2<UserGetDTO>>> GetUsers(
        [FromServices] PaginationService paginationService,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10
    )
    {
        var query = db
            .Users.Select(user => new UserGetDTO
            {
                User = user,
                Roles = (
                    from userRole in db.UserRoles
                    join role in db.Roles on userRole.RoleId equals role.Id
                    where userRole.UserId == user.Id
                    select role.Name
                ).ToList(),
            })
            .OrderByDescending(x => x.Roles.Contains("SUPERADMIN"))
            .ThenByDescending(x => x.User.CreatedAt);

        var paginated = await paginationService.PaginateAsync(query, page, pageSize);

        return Ok(paginated);
    }

    [EndpointSummary("Create User")]
    [EndpointDescription("Creates a new User with the given data & role")]
    [Authorize]
    [HttpPost]
    public async Task<ActionResult> CreateUser([FromBody] UserCreateDTO dto)
    {
        // check email is not already in use
        var existingUser = await userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
            return BadRequest("El correo electrónico ya está en uso");

        // Check sent role exists
        var role = await roleManager.FindByNameAsync(dto.Role);
        if (role == null)
            return BadRequest("El rol solicitado no existe");

        // generate username from name: lowercase, replace spaces with underscores,
        // only alphanumeric characters
        var username = GestionHogar.Model.User.CreateUsername(dto.Name);

        var newUser = new User
        {
            Name = dto.Name,
            UserName = username,
            Email = dto.Email,
            PhoneNumber = dto.Phone,
        };

        var result = await userManager.CreateAsync(newUser, dto.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            logger.LogError("Error creating user: {errors}", errors);
            return StatusCode(500, "Error creating user.");
        }

        var roleResult = await userManager.AddToRoleAsync(newUser, dto.Role);
        if (!roleResult.Succeeded)
        {
            var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
            logger.LogError("Error adding user to role: {errors}", errors);
            return StatusCode(500, "Error adding user to role.");
        }

        // Enviar email de bienvenida
        try
        {
            var welcomeEmailContent = GenerateWelcomeEmailContent(
                newUser.Name,
                dto.Email,
                dto.Password,
                dto.Role
            );
            var emailRequest = new EmailRequest
            {
                To = dto.Email,
                Subject = "Bienvenido a Gestion Hogar",
                Content = welcomeEmailContent,
            };

            await emailService.SendEmailAsync(emailRequest);
            logger.LogInformation("Email de bienvenida enviado a {Email}", dto.Email);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al enviar email de bienvenida a {Email}", dto.Email);
            // No fallamos la creación del usuario si el email falla
        }

        return Ok();
    }

    [EndpointSummary("Update User Data")]
    [EndpointDescription("Updates user data except password")]
    [Authorize]
    [HttpPut("{userId}")]
    public async Task<ActionResult> UpdateUser(Guid userId, [FromBody] UserUpdateDTO dto)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return NotFound("Usuario no encontrado");

        user.Name = dto.Name;
        user.Email = dto.Email;
        user.PhoneNumber = dto.Phone;
        user.UserName = GestionHogar.Model.User.CreateUsername(dto.Name);

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return StatusCode(500, "Error actualizando usuario.");

        // Actualizar rol si cambió
        var currentRoles = await userManager.GetRolesAsync(user);
        if (!currentRoles.Contains(dto.Role))
        {
            await userManager.RemoveFromRolesAsync(user, currentRoles);
            await userManager.AddToRoleAsync(user, dto.Role);
        }

        // Obtener roles actualizados
        var updatedRoles = await userManager.GetRolesAsync(user);

        // Retornar el usuario actualizado y sus roles
        return Ok(new UserGetDTO { User = user, Roles = updatedRoles });
    }

    [EndpointSummary("Update User Password")]
    [EndpointDescription("Updates only the user's password")]
    [Authorize]
    [HttpPut("{userId}/password")]
    public async Task<ActionResult> UpdateUserPassword(
        Guid userId,
        [FromBody] UserUpdatePasswordDTO dto
    )
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return NotFound("Usuario no encontrado");

        // Puedes pedir la contraseña actual si lo deseas (seguridad extra)
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, dto.NewPassword);

        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        // Enviar email de confirmación de cambio de contraseña
        try
        {
            var passwordChangeEmailContent = GeneratePasswordChangeEmailContent(
                user.Name,
                user.Email!
            );
            var emailRequest = new EmailRequest
            {
                To = user.Email!,
                Subject = "Contraseña Actualizada - Gestion Hogar",
                Content = passwordChangeEmailContent,
            };

            await emailService.SendEmailAsync(emailRequest);
            logger.LogInformation("Email de cambio de contraseña enviado a {Email}", user.Email);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error al enviar email de cambio de contraseña a {Email}",
                user.Email
            );
            // No fallamos la actualización si el email falla
        }

        return Ok();
    }

    [EndpointSummary("Deactivate User")]
    [EndpointDescription("Deactivates a single User by its ID")]
    [Authorize]
    [HttpDelete("{userId}")]
    public async Task<IActionResult> DeactivateUser(Guid userId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == userId.ToString())
            return BadRequest("No puedes desactivarte a ti mismo.");

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return NotFound("Usuario no encontrado");

        user.IsActive = false;
        await userManager.UpdateAsync(user);

        return Ok();
    }

    [EndpointSummary("Reactivate User")]
    [EndpointDescription("Reactivates a single User by its ID")]
    [Authorize]
    [HttpPatch("{userId}/reactivate")]
    public async Task<IActionResult> ReactivateUser(Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return NotFound("Usuario no encontrado");

        user.IsActive = true;
        await userManager.UpdateAsync(user);

        // Enviar email de notificación de reactivación
        try
        {
            var reactivationEmailContent = GenerateReactivationEmailContent(user.Name, user.Email!);
            var emailRequest = new EmailRequest
            {
                To = user.Email!,
                Subject = "Cuenta Reactivada - Gestion Hogar",
                Content = reactivationEmailContent,
            };

            await emailService.SendEmailAsync(emailRequest);
            logger.LogInformation("Email de reactivación enviado a {Email}", user.Email);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al enviar email de reactivación a {Email}", user.Email);
            // No fallamos la reactivación si el email falla
        }

        return Ok();
    }

    [EndpointSummary("Update Profile Password")]
    [EndpointDescription("Allows the authenticated user to update their password.")]
    [Authorize]
    [HttpPut("profile/password")]
    public async Task<ActionResult> UpdateProfilePassword([FromBody] UpdateProfilePasswordDTO dto)
    {
        // Validaciones básicas
        if (
            string.IsNullOrWhiteSpace(dto.CurrentPassword)
            || string.IsNullOrWhiteSpace(dto.NewPassword)
            || string.IsNullOrWhiteSpace(dto.ConfirmPassword)
        )
            return BadRequest("Todos los campos son obligatorios.");

        if (dto.NewPassword != dto.ConfirmPassword)
            return BadRequest("La nueva contraseña y la confirmación no coinciden.");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
            return Unauthorized();

        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            return Unauthorized();

        // Verifica la contraseña actual
        var passwordCheck = await userManager.CheckPasswordAsync(user, dto.CurrentPassword);
        if (!passwordCheck)
            return BadRequest("La contraseña actual es incorrecta.");

        // Cambia la contraseña
        var result = await userManager.ChangePasswordAsync(
            user,
            dto.CurrentPassword,
            dto.NewPassword
        );
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        // Enviar email de confirmación de cambio de contraseña
        try
        {
            var passwordChangeEmailContent = GeneratePasswordChangeEmailContent(
                user.Name,
                user.Email!
            );
            var emailRequest = new EmailRequest
            {
                To = user.Email!,
                Subject = "Contraseña Actualizada - Gestion Hogar",
                Content = passwordChangeEmailContent,
            };

            await emailService.SendEmailAsync(emailRequest);
            logger.LogInformation("Email de cambio de contraseña enviado a {Email}", user.Email);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error al enviar email de cambio de contraseña a {Email}",
                user.Email
            );
            // No fallamos la actualización si el email falla
        }

        return Ok(new { message = "Contraseña actualizada correctamente." });
    }

    [EndpointSummary("Get users with higher rank")]
    [EndpointDescription(
        "Gets all users with higher rank (all roles except SaleAdvisor) excluding current user with optional name filter for autocomplete and configurable limit"
    )]
    [Authorize]
    [HttpGet("higher-rank")]
    public async Task<ActionResult<IEnumerable<UserHigherRankDTO>>> GetUsersWithHigherRank(
        [FromQuery] string? name = null,
        [FromQuery] int limit = 10
    )
    {
        try
        {
            var currentUserId = User.GetCurrentUserIdOrThrow();
            var users = await userHigherRankService.GetUsersWithHigherRankAsync(
                currentUserId,
                name,
                limit
            );

            logger.LogInformation(
                "Usuarios con mayor rango obtenidos exitosamente para usuario: {CurrentUserId}, filtro: {Name}, límite: {Limit}",
                currentUserId,
                name ?? "sin filtro",
                limit
            );
            return Ok(users);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized("No se pudo identificar al usuario actual");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al obtener usuarios con mayor rango");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [EndpointSummary("Get users with higher rank paginated")]
    [EndpointDescription(
        "Gets users with higher rank (all roles except SaleAdvisor) excluding current user with pagination"
    )]
    [Authorize]
    [HttpGet("higher-rank/paginated")]
    public async Task<
        ActionResult<PaginatedResponseV2<UserHigherRankDTO>>
    > GetUsersWithHigherRankPaginated([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var currentUserId = User.GetCurrentUserIdOrThrow();
            var result = await userHigherRankService.GetUsersWithHigherRankPaginatedAsync(
                currentUserId,
                page,
                pageSize
            );

            logger.LogInformation(
                "Usuarios con mayor rango paginados obtenidos exitosamente para usuario: {CurrentUserId}, página: {Page}",
                currentUserId,
                page
            );
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized("No se pudo identificar al usuario actual");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al obtener usuarios con mayor rango paginados");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    /// <summary>
    /// Genera el contenido HTML del email de bienvenida
    /// </summary>
    private string GenerateWelcomeEmailContent(
        string userName,
        string email,
        string password,
        string role
    )
    {
        var businessName = businessInfo.Value.Business;
        var businessUrl = businessInfo.Value.Url;

        return $@"
        <h1 style=""color: #1a1a1a; font-weight: 700; font-size: 28px; margin-bottom: 25px; text-align: center;"">
            ¡Bienvenido a {businessName}!
        </h1>
        
        <p style=""font-size: 16px; color: #1a1a1a; margin-bottom: 20px;"">
            Estimado(a) <span class=""highlight"">{userName}</span>,
        </p>
        
        <div class=""info-box"">
            <p style=""font-size: 15px; color: #1a1a1a; margin-bottom: 15px;"">
                Su cuenta ha sido creada exitosamente en la plataforma <span class=""highlight"">{businessName}</span> con el rol de <span class=""highlight"">{role}</span>.
            </p>
        </div>
        
        <div class=""info-box"">
            <h3 style=""color: #1a1a1a; font-weight: 600; margin-bottom: 15px;"">
                Sus credenciales de acceso:
            </h3>
            <ul style=""color: #333333; font-size: 14px; margin: 0; padding-left: 20px;"">
                <li style=""margin-bottom: 8px;""><strong>Email:</strong> <span class=""highlight"">{email}</span></li>
                <li style=""margin-bottom: 8px;""><strong>Contraseña:</strong> <span class=""highlight"">{password}</span></li>
                <li style=""margin-bottom: 8px;""><strong>Rol:</strong> <span class=""highlight"">{role}</span></li>
            </ul>
        </div>
        
        <div class=""info-box"">
            <h3 style=""color: #1a1a1a; font-weight: 600; margin-bottom: 15px;"">
                Información Importante:
            </h3>
            <ul style=""color: #333333; font-size: 14px; margin: 0; padding-left: 20px;"">
                <li style=""margin-bottom: 8px;"">Guarde sus credenciales en un lugar seguro.</li>
                <li style=""margin-bottom: 8px;"">Puede cambiar su contraseña desde su perfil una vez que inicie sesión.</li>
                <li style=""margin-bottom: 8px;"">Si tiene alguna pregunta, contacte al administrador del sistema.</li>
            </ul>
        </div>
        
        <div class=""divider""></div>
        
        <p style=""font-size: 14px; color: #666666; text-align: center; margin-top: 25px;"">
            ¡Esperamos que disfrute usando nuestra plataforma!
        </p>
        
        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{businessUrl}"" class=""btn"">Acceder a la Plataforma</a>
        </div>";
    }

    /// <summary>
    /// Genera el contenido HTML del email de cambio de contraseña
    /// </summary>
    private string GeneratePasswordChangeEmailContent(string userName, string email)
    {
        var businessName = businessInfo.Value.Business;
        var businessUrl = businessInfo.Value.Url;

        return $@"
        <h1 style=""color: #1a1a1a; font-weight: 700; font-size: 28px; margin-bottom: 25px; text-align: center;"">
            Contraseña Actualizada
        </h1>
        
        <p style=""font-size: 16px; color: #1a1a1a; margin-bottom: 20px;"">
            Estimado(a) <span class=""highlight"">{userName}</span>,
        </p>
        
        <div class=""info-box"">
            <p style=""font-size: 15px; color: #1a1a1a; margin-bottom: 15px;"">
                Su contraseña ha sido actualizada exitosamente en la plataforma <span class=""highlight"">{businessName}</span>.
            </p>
        </div>
        
        <div class=""info-box"">
            <h3 style=""color: #1a1a1a; font-weight: 600; margin-bottom: 15px;"">
                Información de la actualización:
            </h3>
            <ul style=""color: #333333; font-size: 14px; margin: 0; padding-left: 20px;"">
                <li style=""margin-bottom: 8px;""><strong>Email:</strong> <span class=""highlight"">{email}</span></li>
                <li style=""margin-bottom: 8px;""><strong>Fecha de actualización:</strong> <span class=""highlight"">{DateTime.Now:dd/MM/yyyy HH:mm}</span></li>
                <li style=""margin-bottom: 8px;""><strong>Estado:</strong> <span class=""highlight"">Actualizada exitosamente</span></li>
            </ul>
        </div>
        
        <div class=""info-box"">
            <h3 style=""color: #1a1a1a; font-weight: 600; margin-bottom: 15px;"">
                Información de Seguridad:
            </h3>
            <ul style=""color: #333333; font-size: 14px; margin: 0; padding-left: 20px;"">
                <li style=""margin-bottom: 8px;"">Si usted no realizó este cambio, contacte inmediatamente al administrador.</li>
                <li style=""margin-bottom: 8px;"">Su nueva contraseña ya está activa y puede usarla para iniciar sesión.</li>
                <li style=""margin-bottom: 8px;"">Recuerde mantener sus credenciales seguras.</li>
            </ul>
        </div>
        
        <div class=""divider""></div>
        
        <p style=""font-size: 14px; color: #666666; text-align: center; margin-top: 25px;"">
            Si tiene alguna consulta, comuníquese con el equipo de soporte de <span class=""highlight"">{businessName}</span>.
        </p>
        
        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{businessUrl}"" class=""btn"">Acceder a la Plataforma</a>
        </div>";
    }

    /// <summary>
    /// Genera el contenido HTML del email de reactivación de cuenta
    /// </summary>
    private string GenerateReactivationEmailContent(string userName, string email)
    {
        var businessName = businessInfo.Value.Business;
        var businessUrl = businessInfo.Value.Url;

        return $@"
        <h1 style=""color: #1a1a1a; font-weight: 700; font-size: 28px; margin-bottom: 25px; text-align: center;"">
            Cuenta Reactivada
        </h1>
        
        <p style=""font-size: 16px; color: #1a1a1a; margin-bottom: 20px;"">
            Estimado(a) <span class=""highlight"">{userName}</span>,
        </p>
        
        <div class=""info-box"">
            <p style=""font-size: 15px; color: #1a1a1a; margin-bottom: 15px;"">
                Su cuenta ha sido reactivada exitosamente en la plataforma <span class=""highlight"">{businessName}</span>.
            </p>
        </div>
        
        <div class=""info-box"">
            <h3 style=""color: #1a1a1a; font-weight: 600; margin-bottom: 15px;"">
                Información de la reactivación:
            </h3>
            <ul style=""color: #333333; font-size: 14px; margin: 0; padding-left: 20px;"">
                <li style=""margin-bottom: 8px;""><strong>Email:</strong> <span class=""highlight"">{email}</span></li>
                <li style=""margin-bottom: 8px;""><strong>Fecha de reactivación:</strong> <span class=""highlight"">{DateTime.Now:dd/MM/yyyy HH:mm}</span></li>
                <li style=""margin-bottom: 8px;""><strong>Estado:</strong> <span class=""highlight"">Cuenta activa</span></li>
            </ul>
        </div>
        
        <div class=""info-box"">
            <h3 style=""color: #1a1a1a; font-weight: 600; margin-bottom: 15px;"">
                ¿Qué significa esto?
            </h3>
            <ul style=""color: #333333; font-size: 14px; margin: 0; padding-left: 20px;"">
                <li style=""margin-bottom: 8px;"">Su cuenta ahora está activa y puede acceder normalmente.</li>
                <li style=""margin-bottom: 8px;"">Puede usar sus credenciales existentes para iniciar sesión.</li>
                <li style=""margin-bottom: 8px;"">Si olvidó su contraseña, puede solicitar un restablecimiento.</li>
            </ul>
        </div>
        
        <div class=""divider""></div>
        
        <p style=""font-size: 14px; color: #666666; text-align: center; margin-top: 25px;"">
            ¡Bienvenido de vuelta a {businessName}!
        </p>
        
        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{businessUrl}"" class=""btn"">Acceder a la Plataforma</a>
        </div>";
    }
}
