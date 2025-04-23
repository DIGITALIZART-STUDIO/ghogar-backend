using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;

namespace GestionHogar.Services;

public interface ILeadService
{
    Task<IEnumerable<Lead>> GetAllLeadsAsync();
    Task<Lead?> GetLeadByIdAsync(Guid id);
    Task<Lead> CreateLeadAsync(Lead lead);
    Task<Lead?> UpdateLeadAsync(Guid id, Lead lead);
    Task<bool> DeleteLeadAsync(Guid id);
    Task<bool> ActivateLeadAsync(Guid id);
    Task<IEnumerable<Lead>> GetInactiveLeadsAsync();
    Task<IEnumerable<Lead>> GetLeadsByClientIdAsync(Guid clientId);
    Task<IEnumerable<Lead>> GetLeadsByAssignedToIdAsync(Guid userId);
    Task<IEnumerable<Lead>> GetLeadsByStatusAsync(LeadStatus status);

    Task<IEnumerable<UserSummaryDto>> GetUsersSummaryAsync();
}
