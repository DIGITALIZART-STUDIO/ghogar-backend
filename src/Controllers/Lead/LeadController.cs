using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;
using GestionHogar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionHogar.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class LeadsController : ControllerBase
{
    private readonly ILeadService _leadService;

    public LeadsController(ILeadService leadService)
    {
        _leadService = leadService;
    }

    // GET: api/leads
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Lead>>> GetLeads()
    {
        var leads = await _leadService.GetAllLeadsAsync();
        return Ok(leads);
    }

    // GET: api/leads/paginated
    [HttpGet("paginated")]
    public async Task<ActionResult<PaginatedResponseV2<Lead>>> GetLeadsPaginated(
        [FromServices] PaginationService paginationService,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10
    )
    {
        var result = await _leadService.GetAllLeadsPaginatedAsync(
            page,
            pageSize,
            paginationService
        );
        return Ok(result);
    }

    // GET: api/leads/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<Lead>> GetLead(Guid id)
    {
        var lead = await _leadService.GetLeadByIdAsync(id);
        if (lead == null)
            return NotFound();

        return Ok(lead);
    }

    // POST: api/leads
    [HttpPost]
    public async Task<ActionResult<Lead>> CreateLead(LeadCreateDto leadDto)
    {
        try
        {
            var lead = new Lead
            {
                ClientId = leadDto.ClientId,
                AssignedToId = leadDto.AssignedToId,
                Status = leadDto.Status,
                CaptureSource = leadDto.CaptureSource,
                ProjectId = leadDto.ProjectId,
            };

            var createdLead = await _leadService.CreateLeadAsync(lead);
            return CreatedAtAction(nameof(GetLead), new { id = createdLead.Id }, createdLead);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // PUT: api/leads/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult<Lead>> UpdateLead(Guid id, LeadUpdateDto leadDto)
    {
        try
        {
            var existingLead = await _leadService.GetLeadByIdAsync(id);
            if (existingLead == null)
                return NotFound();

            // Actualiza solo los campos que no son nulos
            if (leadDto.ClientId.HasValue)
                existingLead.ClientId = leadDto.ClientId;

            if (leadDto.AssignedToId.HasValue)
                existingLead.AssignedToId = leadDto.AssignedToId;

            if (leadDto.ProjectId.HasValue)
                existingLead.ProjectId = leadDto.ProjectId;

            if (leadDto.Status.HasValue)
                existingLead.Status = leadDto.Status.Value;

            if (leadDto.CaptureSource.HasValue)
                existingLead.CaptureSource = leadDto.CaptureSource.Value;

            if (leadDto.CompletionReason.HasValue)
                existingLead.CompletionReason = leadDto.CompletionReason.Value;

            if (leadDto.CancellationReason != null)
                existingLead.CancellationReason = leadDto.CancellationReason;

            var updatedLead = await _leadService.UpdateLeadAsync(id, existingLead);
            return Ok(updatedLead);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // PUT: api/leads/{id}/status
    [HttpPut("{id}/status")]
    public async Task<ActionResult<Lead>> UpdateLeadStatus(Guid id, LeadStatusUpdateDto dto)
    {
        // Validaciones igual que antes...
        if (
            dto.Status != LeadStatus.Registered
            && dto.Status != LeadStatus.Completed
            && dto.Status != LeadStatus.Canceled
        )
        {
            return BadRequest("Solo se permite cambiar a Registered, Completed o Canceled.");
        }

        if (
            (dto.Status == LeadStatus.Completed || dto.Status == LeadStatus.Canceled)
            && dto.CompletionReason == null
        )
        {
            return BadRequest(
                "Debe especificar una razón de finalización para Completed o Canceled."
            );
        }

        var lead = await _leadService.ChangeLeadStatusAsync(id, dto.Status, dto.CompletionReason);
        if (lead == null)
            return NotFound();

        return Ok(lead);
    }

    // DELETE: api/leads/{id}
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteLead(Guid id)
    {
        var success = await _leadService.DeleteLeadAsync(id);
        if (!success)
            return NotFound();

        return NoContent();
    }

    // GET: api/leads/inactive
    [HttpGet("inactive")]
    public async Task<ActionResult<IEnumerable<Lead>>> GetInactiveLeads()
    {
        var leads = await _leadService.GetInactiveLeadsAsync();
        return Ok(leads);
    }

    // GET: api/leads/client/{clientId}
    [HttpGet("client/{clientId}")]
    public async Task<ActionResult<IEnumerable<Lead>>> GetLeadsByClient(Guid clientId)
    {
        var leads = await _leadService.GetLeadsByClientIdAsync(clientId);
        return Ok(leads);
    }

    // GET: api/leads/assignedto/{userId}
    [HttpGet("assignedto/{userId}")]
    public async Task<ActionResult<IEnumerable<Lead>>> GetLeadsByAssignedTo(Guid userId)
    {
        var leads = await _leadService.GetLeadsByAssignedToIdAsync(userId);
        return Ok(leads);
    }

    // GET: api/leads/assignedto/{userId}/paginated
    [HttpGet("assignedto/{userId}/paginated")]
    public async Task<ActionResult<PaginatedResponseV2<Lead>>> GetLeadsByAssignedToPaginated(
        Guid userId,
        [FromServices] PaginationService paginationService,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10
    )
    {
        var result = await _leadService.GetLeadsByAssignedToIdPaginatedAsync(
            userId,
            page,
            pageSize,
            paginationService
        );
        return Ok(result);
    }

    // GET: api/leads/status/{status}
    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<Lead>>> GetLeadsByStatus(LeadStatus status)
    {
        var leads = await _leadService.GetLeadsByStatusAsync(status);
        return Ok(leads);
    }

    // POST: api/leads/{id}/activate
    [HttpPost("{id}/activate")]
    public async Task<ActionResult> ActivateLead(Guid id)
    {
        var success = await _leadService.ActivateLeadAsync(id);
        if (!success)
            return NotFound();

        return NoContent();
    }

    // POST: api/leads/{id}/recycle
    [HttpPost("{id}/recycle")]
    public async Task<ActionResult<Lead>> RecycleLead(Guid id)
    {
        // Obtenemos el ID del usuario actual desde el token JWT
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id" || c.Type == "sub");
        if (userIdClaim == null)
            return BadRequest("No se pudo identificar al usuario actual");

        if (!Guid.TryParse(userIdClaim.Value, out var userId))
            return BadRequest("ID de usuario inválido");

        var lead = await _leadService.RecycleLeadAsync(id, userId);

        if (lead == null)
            return NotFound("No se encontró un lead expirado o cancelado con ese ID");

        return Ok(lead);
    }

    // GET: api/leads/expired
    [HttpGet("expired")]
    public async Task<ActionResult<IEnumerable<Lead>>> GetExpiredLeads()
    {
        var leads = await _leadService.GetExpiredLeadsAsync();
        return Ok(leads);
    }

    // POST: api/leads/check-expired
    [HttpPost("check-expired")]
    [Authorize(Roles = "SuperAdmin,Manager, Admin")]
    public async Task<ActionResult<object>> CheckAndUpdateExpiredLeads()
    {
        var count = await _leadService.CheckAndUpdateExpiredLeadsAsync();
        return Ok(new { expiredLeadsCount = count });
    }

    [HttpDelete("batch")]
    public async Task<ActionResult<BatchOperationResult>> DeleteMultipleLeads(
        [FromBody] IEnumerable<Guid> ids
    )
    {
        if (ids == null || !ids.Any())
            return BadRequest("Se requiere al menos un ID de lead");

        var result = new BatchOperationResult
        {
            SuccessIds = new List<Guid>(),
            FailedIds = new List<Guid>(),
        };

        foreach (var id in ids)
        {
            var success = await _leadService.DeleteLeadAsync(id);
            if (success)
                result.SuccessIds.Add(id);
            else
                result.FailedIds.Add(id);
        }

        return Ok(result);
    }

    // POST: api/leads/batch/activate
    [HttpPost("batch/activate")]
    public async Task<ActionResult<BatchOperationResult>> ActivateMultipleLeads(
        [FromBody] IEnumerable<Guid> ids
    )
    {
        if (ids == null || !ids.Any())
            return BadRequest("Se requiere al menos un ID de lead");

        var result = new BatchOperationResult
        {
            SuccessIds = new List<Guid>(),
            FailedIds = new List<Guid>(),
        };

        foreach (var id in ids)
        {
            var success = await _leadService.ActivateLeadAsync(id);
            if (success)
                result.SuccessIds.Add(id);
            else
                result.FailedIds.Add(id);
        }

        return Ok(result);
    }

    [HttpGet("users/summary")]
    public async Task<ActionResult<IEnumerable<UserSummaryDto>>> GetUsersSummary()
    {
        var usersSummary = await _leadService.GetUsersSummaryAsync();
        return Ok(usersSummary);
    }

    [HttpGet("assigned/{assignedToId:guid}/summary")]
    public async Task<ActionResult<IEnumerable<LeadSummaryDto>>> GetAssignedLeadsSummary(
        Guid assignedToId
    )
    {
        var leads = await _leadService.GetAssignedLeadsSummaryAsync(assignedToId);
        return Ok(leads);
    }

    [HttpGet("assigned/{assignedToId:guid}/available-for-quotation/{excludeQuotationId:guid?}")]
    public async Task<
        ActionResult<IEnumerable<LeadSummaryDto>>
    > GetAvailableLeadsForQuotationByUser(Guid assignedToId, Guid? excludeQuotationId)
    {
        try
        {
            var leads = await _leadService.GetAvailableLeadsForQuotationByUserAsync(
                assignedToId,
                excludeQuotationId
            );
            return Ok(leads);
        }
        catch
        {
            return StatusCode(500, "Error interno del servidor");
        }
    }
}
