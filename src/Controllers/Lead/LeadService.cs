using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Services;

public class LeadService : ILeadService
{
    private readonly DatabaseContext _context;

    public LeadService(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Lead>> GetAllLeadsAsync()
    {
        return await _context.Leads.Include(l => l.Client).Include(l => l.AssignedTo).ToListAsync();
    }

    public async Task<Lead?> GetLeadByIdAsync(Guid id)
    {
        return await _context
            .Leads.Include(l => l.Client)
            .Include(l => l.AssignedTo)
            .FirstOrDefaultAsync(l => l.Id == id && l.IsActive);
    }

    public async Task<Lead> CreateLeadAsync(Lead lead)
    {
        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();
        return lead;
    }

    public async Task<Lead?> UpdateLeadAsync(Guid id, Lead updatedLead)
    {
        var lead = await _context.Leads.FirstOrDefaultAsync(l => l.Id == id && l.IsActive);
        if (lead == null)
            return null;

        lead.ClientId = updatedLead.ClientId;
        lead.AssignedToId = updatedLead.AssignedToId;
        lead.Status = updatedLead.Status;
        lead.Procedency = updatedLead.Procedency;
        lead.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return lead;
    }

    public async Task<bool> DeleteLeadAsync(Guid id)
    {
        var lead = await _context.Leads.FirstOrDefaultAsync(l => l.Id == id && l.IsActive);
        if (lead == null)
            return false;

        lead.IsActive = false;
        lead.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ActivateLeadAsync(Guid id)
    {
        var lead = await _context.Leads.FirstOrDefaultAsync(l => l.Id == id && !l.IsActive);
        if (lead == null)
            return false;

        lead.IsActive = true;
        lead.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<Lead>> GetInactiveLeadsAsync()
    {
        return await _context
            .Leads.Where(l => !l.IsActive)
            .Include(l => l.Client)
            .Include(l => l.AssignedTo)
            .ToListAsync();
    }

    public async Task<IEnumerable<Lead>> GetLeadsByClientIdAsync(Guid clientId)
    {
        return await _context
            .Leads.Where(l => l.IsActive && l.ClientId == clientId)
            .Include(l => l.AssignedTo)
            .ToListAsync();
    }

    public async Task<IEnumerable<Lead>> GetLeadsByAssignedToIdAsync(Guid userId)
    {
        return await _context
            .Leads.Where(l => l.IsActive && l.AssignedToId == userId)
            .Include(l => l.Client)
            .ToListAsync();
    }

    public async Task<IEnumerable<Lead>> GetLeadsByStatusAsync(LeadStatus status)
    {
        return await _context
            .Leads.Where(l => l.IsActive && l.Status == status)
            .Include(l => l.Client)
            .Include(l => l.AssignedTo)
            .ToListAsync();
    }

    public async Task<IEnumerable<UserSummaryDto>> GetUsersSummaryAsync()
    {
        return await _context
            .Users.Select(u => new UserSummaryDto { Id = u.Id, UserName = u.UserName })
            .ToListAsync();
    }
}
