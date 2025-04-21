using System.Security.Claims;
using GestionHogar.Model;
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
    [Authorize(Roles = "SuperAdmin")]
    [HttpGet("all")]
    public async Task<ActionResult<PaginatedResponse<UserGetDTO>>> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10
    )
    {
        // Validate parameters
        if (page < 1)
            page = 1;
        if (pageSize < 1 || pageSize > 100)
            pageSize = 10;

        // Calculate items to skip
        int skip = (page - 1) * pageSize;

        // Get total count
        var totalCount = await db.Users.CountAsync();

        var usersWithRoles = await (
            from user in db.Users
            orderby user.CreatedAt
            select new
            {
                User = user,
                Roles = (
                    from userRole in db.UserRoles
                    join role in db.Roles on userRole.RoleId equals role.Id
                    where userRole.UserId == user.Id
                    select role.Name
                ).ToList(),
            }
        )
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        // Create response
        var response = new PaginatedResponse<UserGetDTO>
        {
            Items = usersWithRoles
                .Select(x => new UserGetDTO { User = x.User, Roles = x.Roles })
                .ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
        };

        return Ok(response);
    }

    [EndpointSummary("Create User")]
    [EndpointDescription("Creates a new User with the given data & role")]
    [Authorize(Roles = "SuperAdmin")]
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

        // FIXME: Auto generate password and send to the frontend
        var result = await userManager.CreateAsync(newUser, "Hogar2025/1");
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

    [EndpointSummary("Deactivate User")]
    [EndpointDescription("Deactivates a single User by its ID")]
    [Authorize(Roles = "SuperAdmin")]
    [HttpDelete("{userId}")]
    public IActionResult DeactivateUser(Guid userId)
    {
        return Ok();
    }

    [EndpointSummary("Batch Deactivate User")]
    [EndpointDescription("Deactivates many users by their IDs")]
    [Authorize(Roles = "SuperAdmin")]
    [HttpDelete]
    public IActionResult BatchDeactivateUser([FromBody] List<Guid> userIds)
    {
        return Ok();
    }

    [EndpointSummary("Reactivate User")]
    [EndpointDescription("Reactivates a single User by its ID")]
    [Authorize(Roles = "SuperAdmin")]
    [HttpPatch("{userId}/reactivate")]
    public IActionResult ReactivateUser(Guid userId)
    {
        return Ok();
    }

    [EndpointSummary("Batch Reactivate User")]
    [EndpointDescription("Reactivates many users by their IDs")]
    [Authorize(Roles = "SuperAdmin")]
    [HttpPatch("batch/reactivate")]
    public IActionResult BatchReactivateUser([FromBody] List<Guid> userIds)
    {
        return Ok();
    }
}
