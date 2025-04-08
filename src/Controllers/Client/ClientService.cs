using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        return await _context.Clients.Where(c => c.IsActive).ToListAsync();
    }

    public async Task<Client?> GetClientByIdAsync(Guid id)
    {
        return await _context.Clients.FirstOrDefaultAsync(c => c.Id == id && c.IsActive);
    }

    public async Task<Client> CreateClientAsync(Client client)
    {
        // Validación básica
        if (!client.ValidateClientData())
        {
            throw new ArgumentException("Datos de cliente inválidos");
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

        // Validación
        if (!client.ValidateClientData())
        {
            throw new ArgumentException("Datos de cliente inválidos");
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
        await _context.SaveChangesAsync();
        return true;
    }

    // Añadir estos métodos al final de la clase ClientService

    public async Task<bool> ActivateClientAsync(Guid id)
    {
        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == id && !c.IsActive);
        if (client == null)
            return false;

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
}
