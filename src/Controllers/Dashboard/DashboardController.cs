using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/dashboard/admin")]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboardService;

    public DashboardController(DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardAdminDto>> GetDashboard([FromQuery] int? year)
    {
        var result = await _dashboardService.GetDashboardAdminDataAsync(year);
        return Ok(result);
    }
}
