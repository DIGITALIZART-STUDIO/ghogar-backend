using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;
using GestionHogar.Services;
using GestionHogar.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ClientsController : ControllerBase
{
    private readonly IClientService _clientService;
    private readonly ILeadService _leadService;
    private readonly DatabaseContext _context;
    private readonly IExcelExportService _excelExportService;
    private readonly OptimizedPaginationService _paginationService;
    private readonly IExcelTemplateService _excelTemplateService;

    public ClientsController(
        IClientService clientService,
        ILeadService leadService,
        DatabaseContext context,
        IExcelExportService excelExportService,
        OptimizedPaginationService paginationService,
        IExcelTemplateService excelTemplateService
    )
    {
        _clientService = clientService;
        _leadService = leadService;
        _context = context;
        _excelExportService = excelExportService;
        _paginationService = paginationService;
        _excelTemplateService = excelTemplateService;
    }

    // GET: api/clients
    [HttpGet]
    public async Task<ActionResult<IEnumerable<GestionHogar.Model.Client>>> GetClients()
    {
        try
        {
            // Obtener el usuario actual y sus roles
            var currentUserId = User.GetCurrentUserIdOrThrow();
            var currentUserRoles = User.GetCurrentUserRoles().ToList();

            // Verificar si es Supervisor
            var isSupervisor = currentUserRoles.Contains("Supervisor");

            // Construir consulta base
            var query = _context.Clients.AsQueryable();

            // FILTRO ESPECIAL PARA SUPERVISORES: Solo mostrar clientes que tienen leads asignados a sus SalesAdvisors o al propio supervisor
            if (isSupervisor)
            {
                // Obtener los IDs de los SalesAdvisors asignados a este supervisor
                var assignedSalesAdvisorIds = await _context
                    .SupervisorSalesAdvisors.Where(ssa =>
                        ssa.SupervisorId == currentUserId && ssa.IsActive
                    )
                    .Select(ssa => ssa.SalesAdvisorId)
                    .ToListAsync();

                // Incluir también el propio ID del supervisor para que vea sus propios clientes
                assignedSalesAdvisorIds.Add(currentUserId);

                // Filtrar clientes que tienen leads asignados a estos usuarios
                query = query.Where(c =>
                    _context.Leads.Any(l =>
                        l.ClientId == c.Id
                        && l.IsActive
                        && (
                            l.AssignedToId.HasValue
                            && (
                                assignedSalesAdvisorIds.Contains(l.AssignedToId.Value)
                                || l.AssignedToId.Value == currentUserId
                            )
                        )
                    )
                );
            }

            var clients = await query.ToListAsync();
            return Ok(clients);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized("No se pudo identificar al usuario actual");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error interno del servidor: {ex.Message}");
        }
    }

    [HttpGet("paginated")]
    public async Task<
        ActionResult<PaginatedResponseV2<GestionHogar.Model.Client>>
    > GetClientsPaginated(
        [FromServices] PaginationService paginationService,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] bool[]? isActive = null,
        [FromQuery] ClientType[]? type = null,
        [FromQuery] string? orderBy = null
    )
    {
        try
        {
            // Obtener el usuario actual y sus roles
            var currentUserId = User.GetCurrentUserIdOrThrow();
            var currentUserRoles = User.GetCurrentUserRoles().ToList();

            // Verificar si es Supervisor
            var isSupervisor = currentUserRoles.Contains("Supervisor");

            var result = await _clientService.GetAllClientsPaginatedAsync(
                page,
                pageSize,
                paginationService,
                search,
                isActive,
                type,
                orderBy,
                currentUserId,
                currentUserRoles,
                isSupervisor
            );
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized("No se pudo identificar al usuario actual");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error interno del servidor: {ex.Message}");
        }
    }

    // GET: api/clients/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<GestionHogar.Model.Client>> GetClient(Guid id)
    {
        var client = await _clientService.GetClientByIdAsync(id);
        if (client == null)
            return NotFound();

        return Ok(client);
    }

    // POST: api/clients

    [HttpPost]
    public async Task<ActionResult<GestionHogar.Model.Client>> CreateClient(
        ClientCreateDto clientDto
    )
    {
        try
        {
            var client = new GestionHogar.Model.Client
            {
                Name = clientDto.Name,
                CoOwners = clientDto.CoOwners, // Cambio de CoOwner a CoOwners
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
                Country = clientDto.Country, // Nuevo campo
                Type = clientDto.Type,
                SeparateProperty = clientDto.SeparateProperty, // Nuevo campo
                SeparatePropertyData = clientDto.SeparatePropertyData, // Nuevo campo
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
    public async Task<ActionResult<GestionHogar.Model.Client>> UpdateClient(
        Guid id,
        ClientUpdateDto clientDto
    )
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

            if (clientDto.CoOwners != null)
                existingClient.CoOwners = clientDto.CoOwners; // Cambio de CoOwner a CoOwners

            if (clientDto.PhoneNumber != null)
                existingClient.PhoneNumber = clientDto.PhoneNumber;

            if (clientDto.Email != null)
                existingClient.Email = clientDto.Email;

            if (clientDto.Address != null)
                existingClient.Address = clientDto.Address;

            if (clientDto.Country != null)
                existingClient.Country = clientDto.Country; // Nuevo campo

            if (clientDto.SeparateProperty.HasValue)
                existingClient.SeparateProperty = clientDto.SeparateProperty.Value; // Nuevo campo

            if (clientDto.SeparatePropertyData != null)
                existingClient.SeparatePropertyData = clientDto.SeparatePropertyData; // Nuevo campo

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
    public async Task<ActionResult<IEnumerable<GestionHogar.Model.Client>>> GetInactiveClients()
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

    // GET: api/clients/current-user/summary
    [HttpGet("current-user/summary")]
    public async Task<ActionResult<IEnumerable<ClientSummaryDto>>> GetClientsByCurrentUserSummary(
        [FromQuery] Guid? projectId = null,
        [FromQuery] bool useCurrentUser = true
    )
    {
        try
        {
            Guid? currentUserId = null;
            Guid? supervisorId = null;
            var currentUserRoles = new List<string>();

            if (useCurrentUser)
            {
                currentUserId = User.GetCurrentUserIdOrThrow();
                supervisorId = currentUserId; // Para compatibilidad con lógica existente
                currentUserRoles = User.GetCurrentUserRoles().ToList();
            }
            else
            {
                // Cuando useCurrentUser=false, obtener el usuario actual para verificar si es supervisor
                currentUserId = User.GetCurrentUserIdOrThrow();
                currentUserRoles = User.GetCurrentUserRoles().ToList();

                // Si el usuario actual es supervisor, usar su ID como supervisorId
                if (currentUserRoles.Contains("Supervisor"))
                {
                    supervisorId = currentUserId;
                }
            }

            // Verificar si es Supervisor
            var isSupervisor = currentUserRoles.Contains("Supervisor");

            var clientsSummary = await _clientService.GetClientsByCurrentUserSummaryAsync(
                currentUserId,
                projectId,
                supervisorId,
                currentUserRoles,
                isSupervisor,
                useCurrentUser
            );
            return Ok(clientsSummary);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized("No se pudo identificar al usuario actual");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error interno del servidor: {ex.Message}");
        }
    }

    // GET: api/clients/paginated-search
    [HttpGet("paginated-search")]
    public async Task<
        ActionResult<PaginatedResponseV2<GestionHogar.Model.Client>>
    > GetClientsPaginated(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? orderBy = null,
        [FromQuery] string? orderDirection = "asc",
        [FromQuery] string? preselectedId = null
    )
    {
        try
        {
            // Validar parámetros
            if (page < 1)
                page = 1;
            if (pageSize < 1 || pageSize > 100)
                pageSize = 10;

            // Obtener el usuario actual y sus roles
            var currentUserId = User.GetCurrentUserIdOrThrow();
            var currentUserRoles = User.GetCurrentUserRoles().ToList();

            // Verificar si es Supervisor
            var isSupervisor = currentUserRoles.Contains("Supervisor");

            // Construir consulta base
            var query = _context.Clients.AsQueryable();

            // FILTRO ESPECIAL PARA SUPERVISORES: Solo mostrar clientes que tienen leads asignados a sus SalesAdvisors o al propio supervisor
            if (isSupervisor)
            {
                // Obtener los IDs de los SalesAdvisors asignados a este supervisor
                var assignedSalesAdvisorIds = await _context
                    .SupervisorSalesAdvisors.Where(ssa =>
                        ssa.SupervisorId == currentUserId && ssa.IsActive
                    )
                    .Select(ssa => ssa.SalesAdvisorId)
                    .ToListAsync();

                // Incluir también el propio ID del supervisor para que vea sus propios clientes
                assignedSalesAdvisorIds.Add(currentUserId);

                // Filtrar clientes que tienen leads asignados a estos usuarios
                query = query.Where(c =>
                    _context.Leads.Any(l =>
                        l.ClientId == c.Id
                        && l.IsActive
                        && (
                            l.AssignedToId.HasValue
                            && (
                                assignedSalesAdvisorIds.Contains(l.AssignedToId.Value)
                                || l.AssignedToId.Value == currentUserId
                            )
                        )
                    )
                );
            }

            // Lógica para preselectedId - incluir en la query base
            Guid? preselectedGuid = null;
            if (
                !string.IsNullOrWhiteSpace(preselectedId)
                && Guid.TryParse(preselectedId, out var parsedGuid)
            )
            {
                preselectedGuid = parsedGuid;

                if (page == 1)
                {
                    // En la primera página: incluir el cliente preseleccionado al inicio
                    var preselectedClient = await _context.Clients.FirstOrDefaultAsync(c =>
                        c.Id == preselectedGuid
                    );

                    if (preselectedClient != null)
                    {
                        // Modificar la query para que el cliente preseleccionado aparezca primero
                        query = query.OrderBy(c => c.Id == preselectedGuid ? 0 : 1);
                    }
                }
                else
                {
                    // En páginas siguientes: excluir el cliente preseleccionado para evitar duplicados
                    query = query.Where(c => c.Id != preselectedGuid);
                }
            }

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
                );
            }

            // Aplicar ordenamiento
            if (!string.IsNullOrWhiteSpace(orderBy))
            {
                var isDescending = orderDirection?.ToLower() == "desc";

                // Si hay preselectedId en la primera página, mantenerlo primero
                if (preselectedGuid.HasValue && page == 1)
                {
                    query = orderBy.ToLower() switch
                    {
                        "name" => isDescending
                            ? query
                                .OrderBy(c => c.Id == preselectedGuid ? 0 : 1)
                                .ThenByDescending(c => c.Name)
                            : query
                                .OrderBy(c => c.Id == preselectedGuid ? 0 : 1)
                                .ThenBy(c => c.Name),
                        "email" => isDescending
                            ? query
                                .OrderBy(c => c.Id == preselectedGuid ? 0 : 1)
                                .ThenByDescending(c => c.Email)
                            : query
                                .OrderBy(c => c.Id == preselectedGuid ? 0 : 1)
                                .ThenBy(c => c.Email),
                        "phonenumber" => isDescending
                            ? query
                                .OrderBy(c => c.Id == preselectedGuid ? 0 : 1)
                                .ThenByDescending(c => c.PhoneNumber)
                            : query
                                .OrderBy(c => c.Id == preselectedGuid ? 0 : 1)
                                .ThenBy(c => c.PhoneNumber),
                        "dni" => isDescending
                            ? query
                                .OrderBy(c => c.Id == preselectedGuid ? 0 : 1)
                                .ThenByDescending(c => c.Dni)
                            : query
                                .OrderBy(c => c.Id == preselectedGuid ? 0 : 1)
                                .ThenBy(c => c.Dni),
                        "ruc" => isDescending
                            ? query
                                .OrderBy(c => c.Id == preselectedGuid ? 0 : 1)
                                .ThenByDescending(c => c.Ruc)
                            : query
                                .OrderBy(c => c.Id == preselectedGuid ? 0 : 1)
                                .ThenBy(c => c.Ruc),
                        "companyname" => isDescending
                            ? query
                                .OrderBy(c => c.Id == preselectedGuid ? 0 : 1)
                                .ThenByDescending(c => c.CompanyName)
                            : query
                                .OrderBy(c => c.Id == preselectedGuid ? 0 : 1)
                                .ThenBy(c => c.CompanyName),
                        "createdat" => isDescending
                            ? query
                                .OrderBy(c => c.Id == preselectedGuid ? 0 : 1)
                                .ThenByDescending(c => c.CreatedAt)
                            : query
                                .OrderBy(c => c.Id == preselectedGuid ? 0 : 1)
                                .ThenBy(c => c.CreatedAt),
                        "modifiedat" => isDescending
                            ? query
                                .OrderBy(c => c.Id == preselectedGuid ? 0 : 1)
                                .ThenByDescending(c => c.ModifiedAt)
                            : query
                                .OrderBy(c => c.Id == preselectedGuid ? 0 : 1)
                                .ThenBy(c => c.ModifiedAt),
                        _ => query
                            .OrderBy(c => c.Id == preselectedGuid ? 0 : 1)
                            .ThenBy(c => c.Name),
                    };
                }
                else
                {
                    query = orderBy.ToLower() switch
                    {
                        "name" => isDescending
                            ? query.OrderByDescending(c => c.Name)
                            : query.OrderBy(c => c.Name),
                        "email" => isDescending
                            ? query.OrderByDescending(c => c.Email)
                            : query.OrderBy(c => c.Email),
                        "phonenumber" => isDescending
                            ? query.OrderByDescending(c => c.PhoneNumber)
                            : query.OrderBy(c => c.PhoneNumber),
                        "dni" => isDescending
                            ? query.OrderByDescending(c => c.Dni)
                            : query.OrderBy(c => c.Dni),
                        "ruc" => isDescending
                            ? query.OrderByDescending(c => c.Ruc)
                            : query.OrderBy(c => c.Ruc),
                        "companyname" => isDescending
                            ? query.OrderByDescending(c => c.CompanyName)
                            : query.OrderBy(c => c.CompanyName),
                        "createdat" => isDescending
                            ? query.OrderByDescending(c => c.CreatedAt)
                            : query.OrderBy(c => c.CreatedAt),
                        "modifiedat" => isDescending
                            ? query.OrderByDescending(c => c.ModifiedAt)
                            : query.OrderBy(c => c.ModifiedAt),
                        _ => query.OrderBy(c => c.Name), // Ordenamiento por defecto
                    };
                }
            }
            else
            {
                // Ordenamiento por defecto
                if (preselectedGuid.HasValue && page == 1)
                {
                    query = query.OrderBy(c => c.Id == preselectedGuid ? 0 : 1).ThenBy(c => c.Name);
                }
                else
                {
                    query = query.OrderBy(c => c.Name);
                }
            }

            // Ejecutar paginación optimizada
            var result = await _paginationService.GetAllPaginatedAsync(
                query,
                page,
                pageSize,
                orderBy,
                null, // filters
                new List<string> { "Referrals" }, // includes
                HttpContext.RequestAborted
            );

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new { message = "Error interno del servidor", error = ex.Message }
            );
        }
    }

    [HttpGet("excel")]
    public async Task<IActionResult> DownloadClientsExcel()
    {
        var clients = await _clientService.GetAllClientsAsync();

        // Encabezados incluyendo los campos complejos con nombres descriptivos
        var headers = new List<string>
        {
            "Nombre",
            "DNI",
            "RUC",
            "Empresa",
            "Teléfono",
            "Email",
            "Dirección",
            "País",
            "Tipo",
            "Activo",
            "CoPropietarios", // Campo complejo - índice 10
            "Propiedad Separada", // Campo complejo - índice 11
        };

        var data = clients
            .Select(c => new List<object>
            {
                c.Name ?? string.Empty,
                c.Dni ?? string.Empty,
                c.Ruc ?? string.Empty,
                c.CompanyName ?? string.Empty,
                c.PhoneNumber ?? string.Empty,
                c.Email ?? string.Empty,
                c.Address ?? string.Empty,
                c.Country ?? string.Empty,
                c.Type != null ? (c.Type ?? ClientType.Juridico).ToString() : string.Empty,
                c.IsActive ? "Sí" : "No",
                ParseJsonArrayOrObject(c.CoOwners ?? string.Empty) ?? string.Empty, // Datos complejos - índice 10
                ParseJsonObject(c.SeparatePropertyData ?? string.Empty) ?? string.Empty, // Datos complejos - índice 11
            })
            .ToList();

        // Especifica qué columnas contienen datos complejos - NO necesario para expansión vertical
        var complexDataColumnIndexes = new List<int>(); // Lista vacía porque usaremos expansión vertical

        var fileBytes = _excelExportService.GenerateExcel(
            "Reporte de Clientes",
            headers,
            data,
            false, // Desactiva la expansión horizontal - usa expansión vertical (filas)
            complexDataColumnIndexes // Lista vacía
        );

        return File(
            fileBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "clientes.xlsx"
        );
    }

    // Métodos auxiliares para procesar JSON
    private object ParseJsonArrayOrObject(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "";

        try
        {
            var doc = JsonDocument.Parse(json);
            // Si es un array u objeto, lo traducimos y devolvemos como JsonElement para que el Excel lo expanda
            if (
                doc.RootElement.ValueKind == JsonValueKind.Array
                || doc.RootElement.ValueKind == JsonValueKind.Object
            )
            {
                // Traducir las claves del JSON y devolver el JsonElement traducido
                return TranslateJsonElement(doc.RootElement, GetCoOwnersMapping());
            }
        }
        catch
        {
            // Si no es JSON, convertimos el string plano a un objeto JSON traducido
            return ConvertPlainTextToTranslatedJson(json, GetCoOwnersMapping());
        }

        return json;
    }

    private object ParseJsonObject(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "";

        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                // Traducir las claves del objeto JSON y devolver el JsonElement traducido
                return TranslateJsonElement(doc.RootElement, GetSeparatePropertyMapping());
            }
        }
        catch
        {
            // Si no es JSON, convertimos el string plano a un objeto JSON traducido
            return ConvertPlainTextToTranslatedJson(json, GetSeparatePropertyMapping());
        }

        return json;
    }

    // NUEVA: Función para convertir texto plano a JSON traducido
    private JsonElement ConvertPlainTextToTranslatedJson(
        string plainText,
        Dictionary<string, string> mapping
    )
    {
        if (string.IsNullOrWhiteSpace(plainText))
            return JsonDocument.Parse("{}").RootElement;

        var jsonObject = new Dictionary<string, object>();

        // Separar por tabuladores o saltos de línea
        var partes = plainText.Split(
            new[] { '\t', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries
        );

        foreach (var parte in partes)
        {
            var kv = parte.Split(new[] { ':' }, 2);
            if (kv.Length == 2)
            {
                var clave = kv[0].Trim();
                var valor = kv[1].Trim();

                // Usar la clave traducida si existe, sino usar la original
                var claveTraducida = mapping.ContainsKey(clave) ? mapping[clave] : clave;
                jsonObject[claveTraducida] = valor;
            }
        }

        // Convertir a JSON y devolver como JsonElement
        var jsonString = System.Text.Json.JsonSerializer.Serialize(jsonObject);
        return JsonDocument.Parse(jsonString).RootElement;
    }

    // NUEVA: Función para traducir JsonElement (JSON válido) con claves en español
    private JsonElement TranslateJsonElement(
        JsonElement element,
        Dictionary<string, string> mapping
    )
    {
        try
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var translatedObject = new Dictionary<string, object>();

                foreach (var property in element.EnumerateObject())
                {
                    var originalKey = property.Name;
                    var translatedKey = mapping.ContainsKey(originalKey)
                        ? mapping[originalKey]
                        : originalKey;

                    // Traducir recursivamente el valor si también es un objeto
                    if (property.Value.ValueKind == JsonValueKind.Object)
                    {
                        translatedObject[translatedKey] = TranslateJsonElement(
                            property.Value,
                            mapping
                        );
                    }
                    else if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        var translatedArray = new List<object>();
                        foreach (var item in property.Value.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.Object)
                            {
                                translatedArray.Add(TranslateJsonElement(item, mapping));
                            }
                            else
                            {
                                var value = GetJsonElementValue(item);
                                if (value != null)
                                    translatedArray.Add(value);
                            }
                        }
                        translatedObject[translatedKey] = translatedArray;
                    }
                    else
                    {
                        var value = GetJsonElementValue(property.Value);
                        if (value != null)
                            translatedObject[translatedKey] = value;
                    }
                }

                var jsonString = System.Text.Json.JsonSerializer.Serialize(translatedObject);
                return JsonDocument.Parse(jsonString).RootElement;
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                var translatedArray = new List<object>();
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        translatedArray.Add(TranslateJsonElement(item, mapping));
                    }
                    else
                    {
                        var value = GetJsonElementValue(item);
                        if (value != null)
                            translatedArray.Add(value);
                    }
                }

                var jsonString = System.Text.Json.JsonSerializer.Serialize(translatedArray);
                return JsonDocument.Parse(jsonString).RootElement;
            }
        }
        catch
        {
            // Si hay algún error, devolver el elemento original
            return element;
        }

        return element;
    }

    // NUEVA: Función auxiliar para obtener el valor de un JsonElement
    private object? GetJsonElementValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString(),
        };
    }

    // NUEVA: Mapeo para co-propietarios
    private Dictionary<string, string> GetCoOwnersMapping()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "dni", "DNI" },
            { "name", "Nombre" },
            { "email", "Email" },
            { "phone", "Teléfono" },
            { "address", "Dirección" },
        };
    }

    // NUEVA: Mapeo para propiedad separada
    private Dictionary<string, string> GetSeparatePropertyMapping()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "email", "Email" },
            { "phone", "Teléfono" },
            { "address", "Dirección" },
            { "spouseDni", "DNI Cónyuge" },
            { "spouseName", "Nombre Cónyuge" },
            { "maritalStatus", "Estado Civil" },
        };
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

                using (var spreadsheetDocument = SpreadsheetDocument.Open(stream, false))
                {
                    var workbookPart = spreadsheetDocument.WorkbookPart;

                    // Obtener siempre la primera hoja (Plantilla Clientes)
                    var sheet = workbookPart.Workbook.Descendants<Sheet>().First();
                    var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id);
                    var sheetData = worksheetPart.Worksheet.Elements<SheetData>().First();
                    var rows = sheetData.Elements<Row>();

                    // Obtenemos la lista de usuarios para mapear nombres a IDs
                    var users = await _leadService.GetUsersSummaryAsync();

                    // Omitir la primera fila (encabezados)
                    var dataRows = rows.Skip(1);

                    foreach (var row in dataRows)
                    {
                        try
                        {
                            var cells = row.Elements<Cell>().ToArray();

                            // Verificar si la fila está vacía (por ejemplo, filas al final del archivo)
                            if (cells.Length == 0 || cells.All(c => c.CellValue == null))
                            {
                                continue; // Saltar filas vacías
                            }

                            // Obtener valores de las celdas
                            string name = GetCellValueByReference(workbookPart, row, "A")?.Trim();
                            string country = GetCellValueByReference(workbookPart, row, "B")
                                ?.Trim(); // Cambiado de coOwner a country
                            string dni = GetCellValueByReference(workbookPart, row, "C")?.Trim();
                            string ruc = GetCellValueByReference(workbookPart, row, "D")?.Trim();
                            string companyName = GetCellValueByReference(workbookPart, row, "E")
                                ?.Trim();
                            string phoneNumber = GetCellValueByReference(workbookPart, row, "F")
                                ?.Trim();
                            string email = GetCellValueByReference(workbookPart, row, "G")?.Trim();
                            string address = GetCellValueByReference(workbookPart, row, "H")
                                ?.Trim();
                            string captureSource =
                                GetCellValueByReference(workbookPart, row, "I")?.Trim() ?? "";
                            string assignedUserName = GetCellValueByReference(
                                workbookPart,
                                row,
                                "J"
                            )
                                ?.Trim();
                            string projectName = GetCellValueByReference(workbookPart, row, "K")
                                ?.Trim();

                            // Si RUC existe pero está vacío, asegurarse de que sea null
                            if (string.IsNullOrWhiteSpace(ruc))
                            {
                                ruc = null;
                            }

                            // Asegurar que el teléfono tenga el prefijo "+"
                            if (
                                !string.IsNullOrWhiteSpace(phoneNumber)
                                && !phoneNumber.StartsWith("+")
                            )
                            {
                                phoneNumber = "+" + phoneNumber;
                            }

                            // Validar datos mínimos requeridos
                            if (string.IsNullOrEmpty(phoneNumber))
                            {
                                importResult.Errors.Add(
                                    $"Fila {row.RowIndex}: El telefono es obligatorio."
                                );
                                continue;
                            }

                            // Determinar el tipo de cliente (natural o jurídico)
                            bool isJuridico = !string.IsNullOrEmpty(ruc);
                            ClientType clientType = isJuridico
                                ? ClientType.Juridico
                                : ClientType.Natural;

                            // Validaciones específicas por tipo de cliente
                            if (isJuridico)
                            {
                                // Validar que el RUC tenga 11 caracteres
                                if (ruc.Length != 11)
                                {
                                    importResult.Errors.Add(
                                        $"Fila {row.RowIndex}: El RUC debe tener 11 caracteres (actual: {ruc})."
                                    );
                                    continue;
                                }
                            }

                            // Procesar nombre de usuario asignado (si existe)
                            Guid? assignedToId = null;
                            if (!string.IsNullOrEmpty(assignedUserName))
                            {
                                // Buscar el usuario por su nombre
                                var user = users.FirstOrDefault(u =>
                                    u.UserName == assignedUserName
                                );
                                if (user != null)
                                {
                                    assignedToId = user.Id;
                                }
                                else
                                {
                                    // Registrar un error si no se encuentra el usuario
                                    importResult.Errors.Add(
                                        $"Fila {row.RowIndex}: No se encontró el usuario '{assignedUserName}'."
                                    );
                                }
                            }

                            // Procesar proyecto (si existe)
                            Guid? projectId = null;
                            if (!string.IsNullOrEmpty(projectName))
                            {
                                // Buscar el proyecto por su nombre
                                var project = await _context.Projects.FirstOrDefaultAsync(p =>
                                    p.Name == projectName && p.IsActive
                                );

                                if (project != null)
                                {
                                    projectId = project.Id;
                                }
                                else
                                {
                                    importResult.Errors.Add(
                                        $"Fila {row.RowIndex}: No se encontró el proyecto '{projectName}' o no está activo."
                                    );
                                }
                            }

                            // Convertir el medio de captación de español a enum
                            LeadCaptureSource captureSourceEnum = LeadCaptureSource.Company; // Valor por defecto

                            Dictionary<string, LeadCaptureSource> captureSourceMapping =
                                new Dictionary<string, LeadCaptureSource>(
                                    StringComparer.OrdinalIgnoreCase
                                )
                                {
                                    { "Empresa", LeadCaptureSource.Company },
                                    { "FB Personal", LeadCaptureSource.PersonalFacebook },
                                    { "Feria inmobiliaria", LeadCaptureSource.RealEstateFair },
                                    { "Institucional", LeadCaptureSource.Institutional },
                                    { "Fidelizado", LeadCaptureSource.Loyalty },
                                };

                            if (
                                !string.IsNullOrEmpty(captureSource)
                                && captureSourceMapping.ContainsKey(captureSource)
                            )
                            {
                                captureSourceEnum = captureSourceMapping[captureSource];
                            }

                            // Verificar si el cliente ya existe por DNI o RUC
                            GestionHogar.Model.Client existingClient = null;
                            if (!string.IsNullOrEmpty(ruc))
                            {
                                existingClient = await _clientService.GetClientByRucAsync(ruc);
                            }
                            else if (!string.IsNullOrEmpty(dni))
                            {
                                existingClient = await _clientService.GetClientByDniAsync(dni);
                            }
                            if (existingClient == null && !string.IsNullOrWhiteSpace(phoneNumber))
                            {
                                existingClient = await _clientService.GetClientByPhoneNumberAsync(
                                    phoneNumber
                                );
                            }

                            Guid clientId;

                            if (existingClient != null)
                            {
                                // Si el cliente ya existe (por RUC, DNI o Teléfono), usamos su ID
                                clientId = existingClient.Id;
                                importResult.ClientsExisting++;
                            }
                            else
                            {
                                // Validar que el teléfono no esté vacío
                                if (string.IsNullOrWhiteSpace(phoneNumber))
                                {
                                    importResult.Errors.Add(
                                        $"Fila {row.RowIndex}: El número de teléfono es obligatorio."
                                    );
                                    continue;
                                }

                                // Crear objeto de cliente
                                var newClient = new GestionHogar.Model.Client
                                {
                                    Name = name,
                                    Country = country,
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
                                    SeparateProperty = false,
                                    SeparatePropertyData = null,
                                    CoOwners = null,
                                };

                                // Crear nuevo cliente
                                var createdClient = await _clientService.CreateClientAsync(
                                    newClient
                                );
                                clientId = createdClient.Id;
                                importResult.ClientsCreated++;
                            }

                            // Generar el código único para el lead antes de crear el objeto
                            var leadCode = await _leadService.GenerateLeadCodeAsync();

                            // Crear Lead para el cliente (siempre, independientemente de si el cliente es nuevo o existente)
                            var lead = new Lead
                            {
                                Code = leadCode, // Ahora sí cumple con el required
                                ClientId = clientId,
                                AssignedToId = assignedToId,
                                Status = LeadStatus.Registered,
                                CaptureSource = captureSourceEnum,
                                ProjectId = projectId,
                            };

                            await _leadService.CreateLeadAsync(lead);
                            importResult.LeadsCreated++;

                            importResult.SuccessCount++;
                        }
                        catch (Exception ex)
                        {
                            // Agregar más detalles sobre el error
                            string errorDetails = $"Error en la fila {row.RowIndex}: ";

                            // Si es una ArgumentException, incluir solo el mensaje de error
                            if (ex is ArgumentException)
                            {
                                errorDetails += ex.Message;
                            }
                            else
                            {
                                // Para otros errores, incluir el tipo de excepción
                                errorDetails += $"{ex.GetType().Name}: {ex.Message}";
                            }

                            importResult.Errors.Add(errorDetails);
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

    // Método auxiliar para obtener el valor de una celda por referencia de columna
    private string GetCellValueByReference(
        DocumentFormat.OpenXml.Packaging.WorkbookPart workbookPart,
        DocumentFormat.OpenXml.Spreadsheet.Row row,
        string columnReference
    )
    {
        // Buscar la celda por su referencia de columna
        var cell = row.Elements<DocumentFormat.OpenXml.Spreadsheet.Cell>()
            .FirstOrDefault(c => c.CellReference.Value.StartsWith(columnReference));

        if (cell == null || cell.CellValue == null)
            return null;

        // Si la celda tiene una referencia a una cadena compartida
        if (
            cell.DataType != null
            && cell.DataType.Value == DocumentFormat.OpenXml.Spreadsheet.CellValues.SharedString
        )
        {
            var stringTable = workbookPart.SharedStringTablePart.SharedStringTable;
            int index = int.Parse(cell.CellValue.Text);
            return stringTable.ElementAt(index).InnerText?.Trim();
        }
        // Si es un valor numérico
        else if (
            cell.DataType == null
            || cell.DataType.Value == DocumentFormat.OpenXml.Spreadsheet.CellValues.Number
        )
        {
            // Si es RUC (columna D)
            if (columnReference == "D")
            {
                // Asegurar formato de 11 dígitos para el RUC
                if (
                    double.TryParse(
                        cell.CellValue.Text,
                        System.Globalization.NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out double numValue
                    )
                )
                {
                    return string.Format("{0:00000000000}", (long)numValue);
                }
            }

            // Para otros números
            if (
                decimal.TryParse(
                    cell.CellValue.Text,
                    System.Globalization.NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out decimal numericValue
                )
            )
            {
                return numericValue.ToString("0", CultureInfo.InvariantCulture);
            }
        }

        return cell.CellValue.Text?.Trim();
    }

    /// <summary>
    /// Descarga la plantilla de importación de clientes en formato Excel
    /// </summary>
    /// <returns>Archivo Excel con plantilla de importación</returns>
    /// <response code="200">Plantilla descargada exitosamente</response>
    /// <response code="500">Error interno del servidor</response>
    [HttpGet("template")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DownloadImportTemplate()
    {
        try
        {
            // Obtener el usuario actual y sus roles usando UserExtensions
            var currentUserId = User.GetCurrentUserIdOrThrow();
            var currentUserRoles = User.GetCurrentUserRoles().ToList();

            var excelBytes = await _excelTemplateService.GenerateClientImportTemplateAsync(
                currentUserId,
                currentUserRoles
            );

            return File(
                excelBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "PlantillaImportacionClientes.xlsx"
            );
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized("No se pudo identificar al usuario actual");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error al generar la plantilla: {ex.Message}");
        }
    }
}
