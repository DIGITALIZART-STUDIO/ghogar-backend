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
    private readonly ILeadService _leadService;

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

    // GET: api/clients/summary
    [HttpGet("summary")]
    public async Task<ActionResult<IEnumerable<ClientSummaryDto>>> GetClientsSummary()
    {
        var clientsSummary = await _clientService.GetClientsSummaryAsync();
        return Ok(clientsSummary);
    }

    // POST: api/clients/import
    [HttpPost("import")]
    public async Task<ActionResult<ImportResult>> ImportClients(IFormFile file)
    {
        if (file == null || file.Length <= 0)
            return BadRequest("No se ha proporcionado ningún archivo.");

        if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Solo se permiten archivos Excel (.xlsx).");

        var importResult = new ImportResult
        {
            SuccessCount = 0,
            ClientsCreated = 0,
            ClientsExisting = 0,
            LeadsCreated = 0,
            Errors = new List<string>(),
        };

        try
        {
            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                stream.Position = 0;

                using (var package = new OfficeOpenXml.ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets[0]; // Primera hoja
                    int rowCount = worksheet.Dimension.Rows;

                    // Empezamos desde la fila 2 asumiendo que la primera fila son encabezados
                    for (int row = 2; row <= rowCount; row++)
                    {
                        try
                        {
                            // Obtener datos de cada columna
                            string name = worksheet.Cells[row, 1].Value?.ToString()?.Trim();
                            string coOwner = worksheet.Cells[row, 2].Value?.ToString()?.Trim();
                            string dni = worksheet.Cells[row, 3].Value?.ToString()?.Trim();
                            string ruc = worksheet.Cells[row, 4].Value?.ToString()?.Trim();
                            string companyName = worksheet.Cells[row, 5].Value?.ToString()?.Trim();
                            string phoneNumber = worksheet.Cells[row, 6].Value?.ToString()?.Trim();
                            string email = worksheet.Cells[row, 7].Value?.ToString()?.Trim();
                            string address = worksheet.Cells[row, 8].Value?.ToString()?.Trim();
                            string procedency =
                                worksheet.Cells[row, 9].Value?.ToString()?.Trim() ?? "";

                            // Validar datos mínimos requeridos
                            if (string.IsNullOrEmpty(name))
                            {
                                importResult.Errors.Add($"Fila {row}: El nombre es obligatorio.");
                                continue;
                            }

                            // Verificar si el cliente ya existe por DNI o RUC
                            Client existingClient = null;
                            if (!string.IsNullOrEmpty(ruc))
                            {
                                existingClient = await _clientService.GetClientByRucAsync(ruc);
                            }
                            else if (!string.IsNullOrEmpty(dni))
                            {
                                existingClient = await _clientService.GetClientByDniAsync(dni);
                            }

                            Guid clientId;

                            if (existingClient != null)
                            {
                                // Si el cliente ya existe, usamos su ID
                                clientId = existingClient.Id;
                                importResult.ClientsExisting++;
                            }
                            else
                            {
                                // Determinar el tipo de cliente
                                ClientType clientType = !string.IsNullOrEmpty(ruc)
                                    ? ClientType.Juridico
                                    : ClientType.Natural;

                                // Crear objeto de cliente
                                var newClient = new Client
                                {
                                    Name = name,
                                    CoOwner = coOwner,
                                    Dni = clientType == ClientType.Natural ? dni : null,
                                    Ruc = clientType == ClientType.Juridico ? ruc : null,
                                    CompanyName =
                                        clientType == ClientType.Juridico
                                            ? (
                                                string.IsNullOrEmpty(companyName)
                                                    ? name
                                                    : companyName
                                            )
                                            : null,
                                    PhoneNumber = phoneNumber,
                                    Email = email,
                                    Address = address,
                                    Type = clientType,
                                };

                                // Crear nuevo cliente
                                var createdClient = await _clientService.CreateClientAsync(
                                    newClient
                                );
                                clientId = createdClient.Id;
                                importResult.ClientsCreated++;
                            }

                            // Crear Lead para el cliente
                            var lead = new Lead
                            {
                                ClientId = clientId,
                                AssignedToId = null,
                                Status = LeadStatus.Registered,
                                Procedency = procedency,
                            };

                            await _leadService.CreateLeadAsync(lead);
                            importResult.LeadsCreated++;
                            importResult.SuccessCount++;
                        }
                        catch (Exception ex)
                        {
                            importResult.Errors.Add($"Error en la fila {row}: {ex.Message}");
                        }
                    }
                }
            }

            return Ok(importResult);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error al procesar el archivo: {ex.Message}");
        }
    }
}
