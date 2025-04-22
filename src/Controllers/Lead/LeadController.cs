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
                Procedency = leadDto.Procedency,
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

            if (leadDto.Status.HasValue)
                existingLead.Status = leadDto.Status.Value;

            if (leadDto.Procedency != null)
                existingLead.Procedency = leadDto.Procedency;

            var updatedLead = await _leadService.UpdateLeadAsync(id, existingLead);
            return Ok(updatedLead);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
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
}
