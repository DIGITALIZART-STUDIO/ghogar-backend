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

    public async Task<PaginatedResponseV2<Client>> GetAllClientsPaginatedAsync(
        int page,
        int pageSize,
        PaginationService paginationService,
        string? search = null,
        bool[]? isActive = null,
        ClientType[]? type = null,
        string? orderBy = null
    )
    {
        // Construir consulta base
        var query = _context.Clients.AsQueryable();

        // Aplicar filtro de búsqueda si se proporciona
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.ToLower();
            query = query.Where(c =>
                (c.Name != null && c.Name.ToLower().Contains(searchTerm))
                || (c.Email != null && c.Email.ToLower().Contains(searchTerm))
                || (c.PhoneNumber != null && c.PhoneNumber.Contains(searchTerm))
                || (c.Dni != null && c.Dni.Contains(searchTerm))
                || (c.Ruc != null && c.Ruc.Contains(searchTerm))
                || (c.CompanyName != null && c.CompanyName.ToLower().Contains(searchTerm))
                || (c.Address != null && c.Address.ToLower().Contains(searchTerm))
                || (c.Country != null && c.Country.ToLower().Contains(searchTerm))
            );
        }

        // Aplicar filtro de isActive si se proporciona
        if (isActive != null && isActive.Length > 0)
        {
            query = query.Where(c => isActive.Contains(c.IsActive));
        }

        // Aplicar filtro de type si se proporciona
        if (type != null && type.Length > 0)
        {
            query = query.Where(c => c.Type.HasValue && type.Contains(c.Type.Value));
        }

        // Aplicar ordenamiento
        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            var orderParts = orderBy.Split(' ');
            var field = orderParts[0].ToLower();
            var direction =
                orderParts.Length > 1 && orderParts[1].ToLower() == "desc" ? "desc" : "asc";

            query = field switch
            {
                "name" => direction == "desc"
                    ? query.OrderByDescending(c => c.Name)
                    : query.OrderBy(c => c.Name),
                "email" => direction == "desc"
                    ? query.OrderByDescending(c => c.Email)
                    : query.OrderBy(c => c.Email),
                "phonenumber" => direction == "desc"
                    ? query.OrderByDescending(c => c.PhoneNumber)
                    : query.OrderBy(c => c.PhoneNumber),
                "dni" => direction == "desc"
                    ? query.OrderByDescending(c => c.Dni)
                    : query.OrderBy(c => c.Dni),
                "ruc" => direction == "desc"
                    ? query.OrderByDescending(c => c.Ruc)
                    : query.OrderBy(c => c.Ruc),
                "companyname" => direction == "desc"
                    ? query.OrderByDescending(c => c.CompanyName)
                    : query.OrderBy(c => c.CompanyName),
                "address" => direction == "desc"
                    ? query.OrderByDescending(c => c.Address)
                    : query.OrderBy(c => c.Address),
                "country" => direction == "desc"
                    ? query.OrderByDescending(c => c.Country)
                    : query.OrderBy(c => c.Country),
                "type" => direction == "desc"
                    ? query.OrderByDescending(c => c.Type)
                    : query.OrderBy(c => c.Type),
                "isactive" => direction == "desc"
                    ? query.OrderByDescending(c => c.IsActive)
                    : query.OrderBy(c => c.IsActive),
                "createdat" => direction == "desc"
                    ? query.OrderByDescending(c => c.CreatedAt)
                    : query.OrderBy(c => c.CreatedAt),
                "modifiedat" => direction == "desc"
                    ? query.OrderByDescending(c => c.ModifiedAt)
                    : query.OrderBy(c => c.ModifiedAt),
                _ => query.OrderBy(c => c.Name), // Ordenamiento por defecto
            };
        }
        else
        {
            // Ordenamiento por defecto
            query = query.OrderBy(c => c.Name);
        }

        return await paginationService.PaginateAsync(query, page, pageSize);
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

        // Verificar que no exista otro cliente con el mismo número de teléfono
        if (!string.IsNullOrEmpty(client.PhoneNumber))
        {
            var existingWithPhone = await _context.Clients.FirstOrDefaultAsync(c =>
                c.PhoneNumber == client.PhoneNumber && c.IsActive
            );
            if (existingWithPhone != null)
            {
                throw new ArgumentException(
                    "Ya existe un cliente activo con este número de teléfono"
                );
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

        // Verificar que no exista otro cliente con el mismo número de teléfono (excluyendo el actual)
        if (!string.IsNullOrEmpty(updatedClient.PhoneNumber))
        {
            var existingWithPhone = await _context.Clients.FirstOrDefaultAsync(c =>
                c.PhoneNumber == updatedClient.PhoneNumber && c.Id != id && c.IsActive
            );
            if (existingWithPhone != null)
            {
                throw new ArgumentException(
                    "Ya existe otro cliente activo con este número de teléfono"
                );
            }
        }

        // Actualizar propiedades
        client.Name = updatedClient.Name;
        client.CoOwners = updatedClient.CoOwners;
        client.Dni = updatedClient.Dni;
        client.Ruc = updatedClient.Ruc;
        client.CompanyName = updatedClient.CompanyName;
        client.PhoneNumber = updatedClient.PhoneNumber;
        client.Email = updatedClient.Email;
        client.Address = updatedClient.Address;
        client.Country = updatedClient.Country;
        client.Type = updatedClient.Type;
        client.SeparateProperty = updatedClient.SeparateProperty;
        client.SeparatePropertyData = updatedClient.SeparatePropertyData;
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

        // Verificar que no exista otro cliente activo con el mismo número de teléfono
        if (!string.IsNullOrEmpty(client.PhoneNumber))
        {
            var existingWithPhone = await _context.Clients.FirstOrDefaultAsync(c =>
                c.PhoneNumber == client.PhoneNumber && c.Id != id && c.IsActive
            );
            if (existingWithPhone != null)
            {
                throw new ArgumentException(
                    "Ya existe otro cliente activo con este número de teléfono"
                );
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
                PhoneNumber = c.PhoneNumber,
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<ClientSummaryDto>> GetClientsByCurrentUserSummaryAsync(
        Guid? currentUserId = null,
        Guid? projectId = null
    )
    {
        var query = _context.Clients.Where(c => c.IsActive).AsQueryable();

        // Aplicar filtro por usuario si se especifica
        if (currentUserId.HasValue)
        {
            query = query.Where(c =>
                _context.Leads.Any(l => l.ClientId == c.Id && l.AssignedToId == currentUserId.Value)
            );
        }

        // Aplicar filtro por proyecto si se especifica
        if (projectId.HasValue)
        {
            if (currentUserId.HasValue)
            {
                query = query.Where(c =>
                    _context.Leads.Any(l =>
                        l.ClientId == c.Id
                        && l.AssignedToId == currentUserId.Value
                        && l.ProjectId == projectId.Value
                    )
                );
            }
            else
            {
                query = query.Where(c =>
                    _context.Leads.Any(l => l.ClientId == c.Id && l.ProjectId == projectId.Value)
                );
            }
        }

        return await query
            .Select(c => new ClientSummaryDto
            {
                Id = c.Id,
                Name = c.Name,
                Dni = c.Dni,
                Ruc = c.Ruc,
                PhoneNumber = c.PhoneNumber,
            })
            .Distinct()
            .ToListAsync();
    }

    public async Task<Client> GetClientByDniAsync(string dni)
    {
        if (string.IsNullOrWhiteSpace(dni))
            return null;

        return await _context.Clients.FirstOrDefaultAsync(c => c.Dni == dni && c.IsActive);
    }

    public async Task<Client> GetClientByPhoneNumberAsync(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return null;

        return await _context.Clients.FirstOrDefaultAsync(c =>
            c.PhoneNumber == phoneNumber && c.IsActive
        );
    }

    public async Task<Client> GetClientByRucAsync(string ruc)
    {
        if (string.IsNullOrWhiteSpace(ruc))
            return null;

        return await _context.Clients.FirstOrDefaultAsync(c => c.Ruc == ruc && c.IsActive);
    }
}
