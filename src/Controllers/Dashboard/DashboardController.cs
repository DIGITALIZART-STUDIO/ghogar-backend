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
    private readonly GetFinanceManagerDashboardDataUseCase _getFinanceManagerDashboardDataUseCase;
    private readonly GetSupervisorDashboardDataUseCase _getSupervisorDashboardDataUseCase;
    private readonly GetManagerDashboardDataUseCase _getManagerDashboardDataUseCase;

    public DashboardController(
        GetDashboardAdminDataUseCase getDashboardAdminDataUseCase,
        GetAdvisorDashboardDataUseCase getAdvisorDashboardDataUseCase,
        GetFinanceManagerDashboardDataUseCase getFinanceManagerDashboardDataUseCase,
        GetSupervisorDashboardDataUseCase getSupervisorDashboardDataUseCase,
        GetManagerDashboardDataUseCase getManagerDashboardDataUseCase
    )
    {
        _getDashboardAdminDataUseCase = getDashboardAdminDataUseCase;
        _getAdvisorDashboardDataUseCase = getAdvisorDashboardDataUseCase;
        _getFinanceManagerDashboardDataUseCase = getFinanceManagerDashboardDataUseCase;
        _getSupervisorDashboardDataUseCase = getSupervisorDashboardDataUseCase;
        _getManagerDashboardDataUseCase = getManagerDashboardDataUseCase;
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

    [HttpGet("finance")]
    [AuthorizeCurrentUser("FinanceManager", "Admin", "SuperAdmin")]
    public async Task<ActionResult<FinanceManagerDashboardDto>> GetFinanceDashboard(
        [FromQuery] int? year,
        [FromQuery] Guid? projectId
    )
    {
        try
        {
            var result = await _getFinanceManagerDashboardDataUseCase.ExecuteAsync(year, projectId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error interno del servidor: {ex.Message}");
        }
    }

    [HttpGet("supervisor")]
    [AuthorizeCurrentUser("Supervisor", "Admin", "SuperAdmin")]
    public async Task<ActionResult<SupervisorDashboardDto>> GetSupervisorDashboard(
        [FromQuery] int? year
    )
    {
        try
        {
            var result = await _getSupervisorDashboardDataUseCase.ExecuteAsync(year);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error interno del servidor: {ex.Message}");
        }
    }

    [HttpGet("manager")]
    [AuthorizeCurrentUser("Manager", "Admin", "SuperAdmin")]
    public async Task<ActionResult<ManagerDashboardDto>> GetManagerDashboard(
        [FromQuery] int? year
    )
    {
        try
        {
            var result = await _getManagerDashboardDataUseCase.ExecuteAsync(year);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error interno del servidor: {ex.Message}");
        }
    }
}
