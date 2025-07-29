using System.Security.Claims;
using GestionHogar.Model;
using GestionHogar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController(
    DatabaseContext db,
    UserManager<User> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    ILogger<UsersController> logger
) : ControllerBase
{
    [EndpointSummary("Get current user")]
    [EndpointDescription("Gets information about the currently logged-in user")]
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<UserGetDTO>> CurrentUser()
    {
        var idClaim = ClaimTypes.NameIdentifier;
        var userId = User.FindFirstValue(idClaim);
        if (userId == null)
            return Unauthorized();
        Guid userGuid;
        try
        {
            userGuid = Guid.Parse(userId);
        }
        catch
        {
            return Unauthorized();
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userGuid);
        if (user == null)
            return Unauthorized();

        var userRoles = await userManager.GetRolesAsync(user);
        if (userRoles == null)
            return Unauthorized();

        return Ok(new UserGetDTO() { User = user, Roles = userRoles });
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

        return Ok(new { message = "Contraseña actualizada correctamente." });
    }
}
