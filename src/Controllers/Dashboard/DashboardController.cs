using GestionHogar.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionHogar.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly GetDashboardAdminDataUseCase _getDashboardAdminDataUseCase;
    private readonly GetAdvisorDashboardDataUseCase _getAdvisorDashboardDataUseCase;

    public DashboardController(
        GetDashboardAdminDataUseCase getDashboardAdminDataUseCase,
        GetAdvisorDashboardDataUseCase getAdvisorDashboardDataUseCase
    )
    {
        _getDashboardAdminDataUseCase = getDashboardAdminDataUseCase;
        _getAdvisorDashboardDataUseCase = getAdvisorDashboardDataUseCase;
    }

    [HttpGet("admin")]
    [AuthorizeCurrentUser]
    public async Task<ActionResult<DashboardAdminDto>> GetDashboard([FromQuery] int? year)
    {
        var result = await _getDashboardAdminDataUseCase.ExecuteAsync(year);
        return Ok(result);
    }

    [HttpGet("advisor")]
    [AuthorizeCurrentUser]
    public async Task<ActionResult<AdvisorDashboardDto>> GetAdvisorDashboard([FromQuery] int? year)
    {
        try
        {
            // Obtener el ID del usuario actual desde el token
            var currentUserId = User.GetCurrentUserIdOrThrow();
            var result = await _getAdvisorDashboardDataUseCase.ExecuteAsync(currentUserId, year);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized("Usuario no autenticado");
        }
    }
}
