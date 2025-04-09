using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GestionHogar.Model;

namespace GestionHogar.Services;

public interface IClientService
{
    Task<IEnumerable<Client>> GetAllClientsAsync();
    Task<Client?> GetClientByIdAsync(Guid id);
    Task<Client> CreateClientAsync(Client client);
    Task<Client?> UpdateClientAsync(Guid id, Client client);
    Task<bool> DeleteClientAsync(Guid id);
    Task<bool> ActivateClientAsync(Guid id);
    Task<IEnumerable<Client>> GetInactiveClientsAsync();
    Task<IEnumerable<Client>> GetClientsByIdsAsync(IEnumerable<Guid> ids, bool activeOnly);
}
