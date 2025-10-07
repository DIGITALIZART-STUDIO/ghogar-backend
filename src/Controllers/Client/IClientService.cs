using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;

namespace GestionHogar.Services;

public interface IClientService
{
    Task<IEnumerable<Client>> GetAllClientsAsync();

    Task<PaginatedResponseV2<Client>> GetAllClientsPaginatedAsync(
        int page,
        int pageSize,
        PaginationService paginationService,
        string? search = null,
        bool[]? isActive = null,
        ClientType[]? type = null,
        string? orderBy = null,
        Guid? currentUserId = null,
        IList<string>? currentUserRoles = null,
        bool isSupervisor = false
    );
    Task<Client?> GetClientByIdAsync(Guid id);
    Task<Client> CreateClientAsync(Client client);
    Task<Client?> UpdateClientAsync(Guid id, Client client);
    Task<bool> DeleteClientAsync(Guid id);
    Task<bool> ActivateClientAsync(Guid id);
    Task<IEnumerable<Client>> GetInactiveClientsAsync();
    Task<IEnumerable<Client>> GetClientsByIdsAsync(IEnumerable<Guid> ids, bool activeOnly);

    Task<IEnumerable<ClientSummaryDto>> GetClientsSummaryAsync();
    Task<IEnumerable<ClientSummaryDto>> GetClientsByCurrentUserSummaryAsync(
        Guid? currentUserId = null,
        Guid? projectId = null,
        Guid? supervisorId = null,
        IList<string>? supervisorRoles = null,
        bool isSupervisor = false,
        bool useCurrentUser = true
    );

    Task<Client> GetClientByDniAsync(string dni);

    Task<Client> GetClientByPhoneNumberAsync(string phoneNumber);
    Task<Client> GetClientByRucAsync(string ruc);
}
