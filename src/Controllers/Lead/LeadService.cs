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
        return await _context
            .Leads.Include(l => l.Client)
            .Include(l => l.AssignedTo)
            .Include(l => l.Project)
            .ToListAsync();
    }

    public async Task<Lead?> GetLeadByIdAsync(Guid id)
    {
        return await _context
            .Leads.Include(l => l.Client)
            .Include(l => l.AssignedTo)
            .Include(l => l.Project)
            .FirstOrDefaultAsync(l => l.Id == id && l.IsActive);
    }

    public async Task<Lead> CreateLeadAsync(Lead lead)
    {
        // Asegurarnos de que se establezcan las fechas correctamente
        lead.EntryDate = DateTime.UtcNow;
        lead.ExpirationDate = DateTime.UtcNow.AddDays(7);

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
        lead.CaptureSource = updatedLead.CaptureSource;
        lead.ProjectId = updatedLead.ProjectId;
        lead.CompletionReason = updatedLead.CompletionReason;

        // Si se está cancelando, guardar el motivo
        if (
            updatedLead.Status == LeadStatus.Canceled
            && !string.IsNullOrEmpty(updatedLead.CancellationReason)
        )
        {
            lead.CancellationReason = updatedLead.CancellationReason;
        }

        lead.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return lead;
    }

    public async Task<Lead?> ToggleLeadStatusAsync(Guid id)
    {
        var lead = await _context.Leads.FirstOrDefaultAsync(l => l.Id == id && l.IsActive);
        if (lead == null)
            return null;

        // Cambiar el estado: si es Registered, cambia a Attended y viceversa
        lead.Status =
            lead.Status == LeadStatus.Registered ? LeadStatus.Attended : LeadStatus.Registered;
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
            .Include(l => l.Project)
            .ToListAsync();
    }

    public async Task<IEnumerable<Lead>> GetLeadsByClientIdAsync(Guid clientId)
    {
        return await _context
            .Leads.Where(l => l.IsActive && l.ClientId == clientId)
            .Include(l => l.AssignedTo)
            .Include(l => l.Project)
            .ToListAsync();
    }

    public async Task<IEnumerable<Lead>> GetLeadsByAssignedToIdAsync(Guid userId)
    {
        return await _context
            .Leads.Where(l => l.IsActive && l.AssignedToId == userId)
            .Include(l => l.Client)
            .Include(l => l.Project)
            .ToListAsync();
    }

    public async Task<IEnumerable<Lead>> GetLeadsByStatusAsync(LeadStatus status)
    {
        return await _context
            .Leads.Where(l => l.IsActive && l.Status == status)
            .Include(l => l.Client)
            .Include(l => l.AssignedTo)
            .Include(l => l.Project)
            .ToListAsync();
    }

    public async Task<IEnumerable<UserSummaryDto>> GetUsersSummaryAsync()
    {
        var salesAdvisorRoleId = await _context
            .Roles.Where(r => r.Name == "SalesAdvisor")
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        return await _context
            .Users.Where(u =>
                u.IsActive
                && _context.UserRoles.Any(ur =>
                    ur.UserId == u.Id && ur.RoleId == salesAdvisorRoleId
                )
            )
            .Select(u => new UserSummaryDto { Id = u.Id, UserName = u.UserName })
            .ToListAsync();
    }

    public async Task<IEnumerable<LeadSummaryDto>> GetAssignedLeadsSummaryAsync(Guid assignedToId)
    {
        var leads = await _context
            .Leads.Where(l => l.IsActive && l.AssignedToId == assignedToId)
            .Include(l => l.Client)
            .Include(l => l.Project)
            .ToListAsync();

        return leads.Select(LeadSummaryDto.FromEntity);
    }

    // Nuevos métodos para manejar reciclaje y expiración

    public async Task<Lead?> RecycleLeadAsync(Guid id, Guid userId)
    {
        var lead = await _context.Leads.FirstOrDefaultAsync(l =>
            l.Id == id
            && l.IsActive
            && (l.Status == LeadStatus.Expired || l.Status == LeadStatus.Canceled)
        );

        if (lead == null)
            return null;

        lead.RecycleLead(userId);

        await _context.SaveChangesAsync();

        return lead;
    }

    public async Task<IEnumerable<Lead>> GetExpiredLeadsAsync()
    {
        return await _context
            .Leads.Where(l => l.IsActive && l.Status == LeadStatus.Expired)
            .Include(l => l.Client)
            .Include(l => l.AssignedTo)
            .Include(l => l.Project)
            .ToListAsync();
    }

    public async Task<int> CheckAndUpdateExpiredLeadsAsync()
    {
        var now = DateTime.UtcNow;

        // Buscar leads activos con fecha de expiración anterior a ahora y que no estén ya marcados como expirados
        var expiredLeads = await _context
            .Leads.Where(l =>
                l.IsActive
                && l.ExpirationDate < now
                && l.Status != LeadStatus.Expired
                && l.Status != LeadStatus.Completed
                && l.Status != LeadStatus.Canceled
            )
            .ToListAsync();

        foreach (var lead in expiredLeads)
        {
            lead.Status = LeadStatus.Expired;
            lead.ModifiedAt = now;
        }

        await _context.SaveChangesAsync();
        return expiredLeads.Count;
    }
}
