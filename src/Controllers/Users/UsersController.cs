using System.Security.Claims;
using GestionHogar.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Controllers;

[ApiController]
[Authorize(Roles = "SuperAdmin")]
[Route("api/[controller]")]
public class UsersController(DatabaseContext db) : ControllerBase
{
    [EndpointSummary("Get current user")]
    [EndpointDescription("Gets information about the currently logged-in user")]
    [HttpGet]
    public async Task<ActionResult<User>> CurrentUser()
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

        return Ok(user);
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
