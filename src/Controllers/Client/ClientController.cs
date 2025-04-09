using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;
using GestionHogar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionHogar.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ClientsController : ControllerBase
{
    private readonly IClientService _clientService;

    public ClientsController(IClientService clientService)
    {
        _clientService = clientService;
    }

    // GET: api/clients
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Client>>> GetClients()
    {
        var clients = await _clientService.GetAllClientsAsync();
        return Ok(clients);
    }

    // GET: api/clients/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<Client>> GetClient(Guid id)
    {
        var client = await _clientService.GetClientByIdAsync(id);
        if (client == null)
            return NotFound();

        return Ok(client);
    }

    // POST: api/clients
    [HttpPost]
    public async Task<ActionResult<Client>> CreateClient(ClientCreateDto clientDto)
    {
        try
        {
            var client = new Client
            {
                Name = clientDto.Name,
                CoOwner = clientDto.CoOwner,
                Dni = clientDto.Dni,
                Ruc = clientDto.Ruc,
                // Para clientes jurídicos, si no hay CompanyName, usar Name
                CompanyName =
                    clientDto.Type == ClientType.Juridico
                    && string.IsNullOrEmpty(clientDto.CompanyName)
                        ? clientDto.Name
                        : clientDto.CompanyName,
                PhoneNumber = clientDto.PhoneNumber,
                Email = clientDto.Email,
                Address = clientDto.Address,
                Type = clientDto.Type,
            };

            var createdClient = await _clientService.CreateClientAsync(client);
            return CreatedAtAction(nameof(GetClient), new { id = createdClient.Id }, createdClient);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // PUT: api/clients/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult<Client>> UpdateClient(Guid id, ClientUpdateDto clientDto)
    {
        try
        {
            var existingClient = await _clientService.GetClientByIdAsync(id);
            if (existingClient == null)
                return NotFound();

            // Si hay cambio de tipo de cliente, limpiar campos que ya no aplican
            if (clientDto.Type.HasValue && clientDto.Type.Value != existingClient.Type)
            {
                if (clientDto.Type.Value == ClientType.Natural)
                {
                    // Si cambia a Natural, limpiar campos de Jurídico
                    existingClient.Ruc = null;
                    existingClient.CompanyName = null;
                }
                else // Cambio a Jurídico
                {
                    // Si cambia a Jurídico, limpiar campos de Natural y asignar Name a CompanyName
                    existingClient.Dni = null;
                    existingClient.CompanyName = existingClient.Name;
                }
            }

            // Actualiza solo los campos que no son nulos
            if (clientDto.Name != null)
            {
                existingClient.Name = clientDto.Name;

                // Si es jurídico y CompanyName está vacío, actualizarlo también
                if (
                    existingClient.Type == ClientType.Juridico
                    && string.IsNullOrEmpty(existingClient.CompanyName)
                )
                    existingClient.CompanyName = clientDto.Name;
            }

            if (clientDto.CoOwner != null)
                existingClient.CoOwner = clientDto.CoOwner;
            if (clientDto.PhoneNumber != null)
                existingClient.PhoneNumber = clientDto.PhoneNumber;
            if (clientDto.Email != null)
                existingClient.Email = clientDto.Email;
            if (clientDto.Address != null)
                existingClient.Address = clientDto.Address;

            // Actualizar campos específicos según el tipo
            if (clientDto.Type.HasValue)
                existingClient.Type = clientDto.Type.Value;

            // Campos específicos por tipo
            if (existingClient.Type == ClientType.Natural && clientDto.Dni != null)
                existingClient.Dni = clientDto.Dni;
            else if (existingClient.Type == ClientType.Juridico)
            {
                if (clientDto.Ruc != null)
                    existingClient.Ruc = clientDto.Ruc;

                // Si se proporciona CompanyName, usarlo; de lo contrario, si está vacío, usar Name
                if (clientDto.CompanyName != null)
                    existingClient.CompanyName = clientDto.CompanyName;
                else if (string.IsNullOrEmpty(existingClient.CompanyName) && clientDto.Name != null)
                    existingClient.CompanyName = clientDto.Name;
            }

            var updatedClient = await _clientService.UpdateClientAsync(id, existingClient);
            return Ok(updatedClient);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // DELETE: api/clients/batch
    [HttpDelete("batch")]
    public async Task<ActionResult<BatchOperationResult>> DeleteMultipleClients(
        [FromBody] IEnumerable<Guid> ids
    )
    {
        if (ids == null || !ids.Any())
            return BadRequest("Se requiere al menos un ID de cliente");

        var result = new BatchOperationResult
        {
            SuccessIds = new List<Guid>(),
            FailedIds = new List<Guid>(),
        };

        // Obtenemos todos los clientes activos con esos IDs en una sola consulta
        var clientsToDeactivate = await _clientService.GetClientsByIdsAsync(ids, true);

        // Desactivamos cada cliente
        foreach (var id in ids)
        {
            var client = clientsToDeactivate.FirstOrDefault(c => c.Id == id);

            // Si el cliente existe y está activo, lo desactivamos
            if (client != null)
            {
                var success = await _clientService.DeleteClientAsync(id);
                if (success)
                    result.SuccessIds.Add(id);
                else
                    result.FailedIds.Add(id);
            }
            else
            {
                // El cliente no existe o ya está inactivo
                result.FailedIds.Add(id);
            }
        }

        return Ok(result);
    }

    // POST: api/clients/batch/activate
    [HttpPost("batch/activate")]
    public async Task<ActionResult<BatchOperationResult>> ActivateMultipleClients(
        [FromBody] IEnumerable<Guid> ids
    )
    {
        if (ids == null || !ids.Any())
            return BadRequest("Se requiere al menos un ID de cliente");

        var result = new BatchOperationResult
        {
            SuccessIds = new List<Guid>(),
            FailedIds = new List<Guid>(),
        };

        // Obtenemos todos los clientes inactivos con esos IDs en una sola consulta
        var clientsToActivate = await _clientService.GetClientsByIdsAsync(ids, false);

        // Activamos cada cliente
        foreach (var id in ids)
        {
            var client = clientsToActivate.FirstOrDefault(c => c.Id == id);

            // Si el cliente existe y está inactivo, lo activamos
            if (client != null)
            {
                var success = await _clientService.ActivateClientAsync(id);
                if (success)
                    result.SuccessIds.Add(id);
                else
                    result.FailedIds.Add(id);
            }
            else
            {
                // El cliente no existe o ya está activo
                result.FailedIds.Add(id);
            }
        }

        return Ok(result);
    }

    // GET: api/clients/inactive
    [HttpGet("inactive")]
    public async Task<ActionResult<IEnumerable<Client>>> GetInactiveClients()
    {
        var clients = await _clientService.GetInactiveClientsAsync();
        return Ok(clients);
    }
}
