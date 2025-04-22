using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Services;

public class ClientService : IClientService
{
    private readonly DatabaseContext _context;

    public ClientService(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Client>> GetAllClientsAsync()
    {
        return await _context.Clients.ToListAsync();
    }

    public async Task<Client?> GetClientByIdAsync(Guid id)
    {
        return await _context.Clients.FirstOrDefaultAsync(c => c.Id == id && c.IsActive);
    }

    public async Task<Client> CreateClientAsync(Client client)
    {
        // Validación básica
        var (isValid, errorMessage) = client.ValidateClientDetails();
        if (!isValid)
        {
            throw new ArgumentException(errorMessage);
        }

        // Verificar que no exista otro cliente con el mismo DNI
        if (client.Type == ClientType.Natural && !string.IsNullOrEmpty(client.Dni))
        {
            var existingWithDni = await _context.Clients.FirstOrDefaultAsync(c =>
                c.Dni == client.Dni && c.IsActive
            );
            if (existingWithDni != null)
            {
                throw new ArgumentException("Ya existe un cliente activo con este DNI");
            }
        }

        // Verificar que no exista otro cliente con el mismo RUC
        if (client.Type == ClientType.Juridico && !string.IsNullOrEmpty(client.Ruc))
        {
            var existingWithRuc = await _context.Clients.FirstOrDefaultAsync(c =>
                c.Ruc == client.Ruc && c.IsActive
            );
            if (existingWithRuc != null)
            {
                throw new ArgumentException("Ya existe un cliente activo con este RUC");
            }
        }

        _context.Clients.Add(client);
        await _context.SaveChangesAsync();
        return client;
    }

    public async Task<Client?> UpdateClientAsync(Guid id, Client updatedClient)
    {
        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == id && c.IsActive);
        if (client == null)
            return null;

        // Verificar que no exista otro cliente con el mismo DNI (excluyendo el actual)
        if (updatedClient.Type == ClientType.Natural && !string.IsNullOrEmpty(updatedClient.Dni))
        {
            // Buscar cualquier cliente con el mismo DNI (activo o inactivo)
            var existingWithDni = await _context.Clients.FirstOrDefaultAsync(c =>
                c.Dni == updatedClient.Dni && c.Id != id
            );

            if (existingWithDni != null)
            {
                if (!existingWithDni.IsActive)
                {
                    throw new ArgumentException(
                        "Este DNI ya existe pero pertenece a un cliente inactivo"
                    );
                }
                else
                {
                    throw new ArgumentException("Ya existe otro cliente activo con este DNI");
                }
            }
        }

        // Verificar que no exista otro cliente con el mismo RUC (excluyendo el actual)
        if (updatedClient.Type == ClientType.Juridico && !string.IsNullOrEmpty(updatedClient.Ruc))
        {
            // Buscar cualquier cliente con el mismo RUC (activo o inactivo)
            var existingWithRuc = await _context.Clients.FirstOrDefaultAsync(c =>
                c.Ruc == updatedClient.Ruc && c.Id != id
            );

            if (existingWithRuc != null)
            {
                if (!existingWithRuc.IsActive)
                {
                    throw new ArgumentException(
                        "Este RUC ya existe pero pertenece a un cliente inactivo"
                    );
                }
                else
                {
                    throw new ArgumentException("Ya existe otro cliente activo con este RUC");
                }
            }
        }

        // Actualizar propiedades
        client.Name = updatedClient.Name;
        client.CoOwner = updatedClient.CoOwner;
        client.Dni = updatedClient.Dni;
        client.Ruc = updatedClient.Ruc;
        client.CompanyName = updatedClient.CompanyName;
        client.PhoneNumber = updatedClient.PhoneNumber;
        client.Email = updatedClient.Email;
        client.Address = updatedClient.Address;
        client.Type = updatedClient.Type;
        client.ModifiedAt = DateTime.UtcNow;

        // Validación
        var (isValid, errorMessage) = client.ValidateClientDetails();
        if (!isValid)
        {
            throw new ArgumentException(errorMessage);
        }

        await _context.SaveChangesAsync();
        return client;
    }

    public async Task<bool> DeleteClientAsync(Guid id)
    {
        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == id && c.IsActive);
        if (client == null)
            return false;

        // Borrado lógico
        client.IsActive = false;
        client.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ActivateClientAsync(Guid id)
    {
        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == id && !c.IsActive);
        if (client == null)
            return false;

        // Verificar que no exista otro cliente activo con el mismo DNI
        if (client.Type == ClientType.Natural && !string.IsNullOrEmpty(client.Dni))
        {
            var existingWithDni = await _context.Clients.FirstOrDefaultAsync(c =>
                c.Dni == client.Dni && c.Id != id && c.IsActive
            );
            if (existingWithDni != null)
            {
                throw new ArgumentException("Ya existe otro cliente activo con este DNI");
            }
        }

        // Verificar que no exista otro cliente activo con el mismo RUC
        if (client.Type == ClientType.Juridico && !string.IsNullOrEmpty(client.Ruc))
        {
            var existingWithRuc = await _context.Clients.FirstOrDefaultAsync(c =>
                c.Ruc == client.Ruc && c.Id != id && c.IsActive
            );
            if (existingWithRuc != null)
            {
                throw new ArgumentException("Ya existe otro cliente activo con este RUC");
            }
        }

        // Activar cliente
        client.IsActive = true;
        client.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<Client>> GetInactiveClientsAsync()
    {
        return await _context.Clients.Where(c => !c.IsActive).ToListAsync();
    }

    public async Task<IEnumerable<Client>> GetClientsByIdsAsync(
        IEnumerable<Guid> ids,
        bool activeOnly
    )
    {
        var query = _context.Clients.Where(c => ids.Contains(c.Id));

        if (activeOnly)
            query = query.Where(c => c.IsActive);
        else
            query = query.Where(c => !c.IsActive);

        return await query.ToListAsync();
    }

    public async Task<IEnumerable<ClientSummaryDto>> GetClientsSummaryAsync()
    {
        return await _context
            .Clients.Where(c => c.IsActive)
            .Select(c => new ClientSummaryDto
            {
                Id = c.Id,
                Name = c.Name,
                Dni = c.Dni,
                Ruc = c.Ruc,
            })
            .ToListAsync();
    }
}
