using GestionHogar.Controllers.Dtos;
using GestionHogar.Services;
using Microsoft.AspNetCore.Mvc;

namespace GestionHogar.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LandingController : ControllerBase
{
    private readonly ILandingService _landingService;
    private readonly ILogger<LandingController> _logger;

    public LandingController(ILandingService landingService, ILogger<LandingController> logger)
    {
        _landingService = landingService;
        _logger = logger;
    }

    // POST: api/landing/referral
    [HttpPost("referral")]
    public async Task<ActionResult<ReferralResultDto>> CreateReferral(ReferralCreateDto referralDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validar que no sea auto-referencia
            if (referralDto.Referrer.Telefono == referralDto.Referred.Telefono)
            {
                return BadRequest("No se puede referir a sí mismo");
            }

            var result = await _landingService.ProcessReferralFromLandingAsync(referralDto);

            _logger.LogInformation(
                "Referral creado exitosamente desde landing. ReferralId: {ReferralId}",
                result.ReferralId
            );

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Error de validación al crear referral desde landing");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error interno al crear referral desde landing");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    // POST: api/landing/contact
    [HttpPost("contact")]
    public async Task<ActionResult<ContactResultDto>> CreateContact(ContactCreateDto contactDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _landingService.ProcessContactFromLandingAsync(contactDto);

            _logger.LogInformation(
                "Contacto creado exitosamente desde landing. ClientId: {ClientId}, LeadId: {LeadId}",
                result.ClientId,
                result.LeadId
            );

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Error de validación al crear contacto desde landing");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error interno al crear contacto desde landing");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    // GET: api/landing/projects
    [HttpGet("projects")]
    public async Task<ActionResult<IEnumerable<object>>> GetActiveProjects()
    {
        try
        {
            var projects = await _landingService.GetActiveProjectsAsync();

            var result = projects.Select(p => new
            {
                id = p.Id,
                name = p.Name,
                location = p.Location,
                currency = p.Currency,
                defaultDownPayment = p.DefaultDownPayment,
                defaultFinancingMonths = p.DefaultFinancingMonths,
                maxDiscountPercentage = p.MaxDiscountPercentage,
                projectUrlImage = p.ProjectUrlImage,
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener proyectos activos");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    // GET: api/landing/health
    [HttpGet("health")]
    public ActionResult<object> HealthCheck()
    {
        return Ok(
            new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                service = "Landing API",
            }
        );
    }
}
