using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;

namespace GestionHogar.Services;

public interface IClientService
{
    Task<IEnumerable<Client>> GetAllClientsAsync();

    Task<PaginatedResponseV2<Client>> GetAllClientsPaginatedAsync(
        int page,
        int pageSize,
        PaginationService paginationService
    );
    Task<Client?> GetClientByIdAsync(Guid id);
    Task<Client> CreateClientAsync(Client client);
    Task<Client?> UpdateClientAsync(Guid id, Client client);
    Task<bool> DeleteClientAsync(Guid id);
    Task<bool> ActivateClientAsync(Guid id);
    Task<IEnumerable<Client>> GetInactiveClientsAsync();
    Task<IEnumerable<Client>> GetClientsByIdsAsync(IEnumerable<Guid> ids, bool activeOnly);

    Task<IEnumerable<ClientSummaryDto>> GetClientsSummaryAsync();

    Task<Client> GetClientByDniAsync(string dni);

    Task<Client> GetClientByPhoneNumberAsync(string phoneNumber);
    Task<Client> GetClientByRucAsync(string ruc);
}
