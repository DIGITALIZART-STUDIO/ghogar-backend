using System.Security.Claims;
using GestionHogar.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Controllers;

[ApiController]
[Authorize(Roles = "SuperAdmin")]
[Route("api/[controller]")]
public class UsersController(DatabaseContext db, UserManager<User> userManager) : ControllerBase
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
    [HttpPost]
    public IActionResult CreateUser()
    {
        return Ok();
    }

    [EndpointSummary("Deactivate User")]
    [EndpointDescription("Deactivates a single User by its ID")]
    [HttpDelete("{userId}")]
    public IActionResult DeactivateUser(Guid userId)
    {
        return Ok();
    }

    [EndpointSummary("Batch Deactivate User")]
    [EndpointDescription("Deactivates many users by their IDs")]
    [HttpDelete]
    public IActionResult BatchDeactivateUser([FromBody] List<Guid> userIds)
    {
        return Ok();
    }

    [EndpointSummary("Reactivate User")]
    [EndpointDescription("Reactivates a single User by its ID")]
    [HttpPatch("{userId}/reactivate")]
    public IActionResult ReactivateUser(Guid userId)
    {
        return Ok();
    }

    [EndpointSummary("Batch Reactivate User")]
    [EndpointDescription("Reactivates many users by their IDs")]
    [HttpPatch("batch/reactivate")]
    public IActionResult BatchReactivateUser([FromBody] List<Guid> userIds)
    {
        return Ok();
    }
}
