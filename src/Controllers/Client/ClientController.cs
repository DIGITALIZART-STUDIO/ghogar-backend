using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;
using GestionHogar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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

    public ClientsController(
        IClientService clientService,
        ILeadService leadService,
        DatabaseContext context
    )
    {
        _clientService = clientService;
        _leadService = leadService;
        _context = context;
    }

    // GET: api/clients
    [HttpGet]
    public async Task<ActionResult<IEnumerable<GestionHogar.Model.Client>>> GetClients()
    {
        var clients = await _clientService.GetAllClientsAsync();
        return Ok(clients);
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

                            Guid clientId;

                            if (existingClient != null)
                            {
                                // Si el cliente ya existe, usamos su ID
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

                                // Verificar que el teléfono sea único
                                var existingWithPhone =
                                    await _clientService.GetClientByPhoneNumberAsync(phoneNumber);
                                if (existingWithPhone != null)
                                {
                                    importResult.Errors.Add(
                                        $"Fila {row.RowIndex}: Ya existe un cliente con el número de teléfono {phoneNumber}."
                                    );
                                    continue;
                                }

                                // Crear objeto de cliente
                                var newClient = new GestionHogar.Model.Client
                                {
                                    Name = name,
                                    Country = country, // Usar campo Country en vez de CoOwner
                                    // Solo asignar DNI para clientes naturales
                                    Dni = clientType == ClientType.Natural ? dni : null,
                                    // Solo asignar RUC para clientes jurídicos
                                    Ruc = clientType == ClientType.Juridico ? ruc : null,
                                    // Solo asignar CompanyName para clientes jurídicos
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
                                    SeparateProperty = false, // Valor predeterminado para separación de bienes
                                    SeparatePropertyData = null, // Valor predeterminado para datos de separación
                                    CoOwners = null, // Valor predeterminado para copropietarios
                                };

                                // Crear nuevo cliente
                                var createdClient = await _clientService.CreateClientAsync(
                                    newClient
                                );
                                clientId = createdClient.Id;
                                importResult.ClientsCreated++;
                            }

                            // Crear Lead para el cliente (siempre, independientemente de si el cliente es nuevo o existente)
                            if (assignedToId.HasValue)
                            {
                                var lead = new Lead
                                {
                                    ClientId = clientId,
                                    AssignedToId = assignedToId.Value,
                                    Status = LeadStatus.Registered,
                                    CaptureSource = captureSourceEnum,
                                    ProjectId = projectId,
                                };

                                await _leadService.CreateLeadAsync(lead);
                                importResult.LeadsCreated++;
                            }
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

    // GET: api/clients/template
    [HttpGet("template")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DownloadImportTemplate()
    {
        try
        {
            // Crear stream en memoria para el documento Excel
            var stream = new MemoryStream();

            // Crear el documento Excel
            using (
                var spreadsheetDocument = SpreadsheetDocument.Create(
                    stream,
                    SpreadsheetDocumentType.Workbook
                )
            )
            {
                // Crear partes del documento
                var workbookPart = spreadsheetDocument.AddWorkbookPart();
                workbookPart.Workbook = new Workbook();

                // PRIMERO: Crear la hoja de estilos UNA SOLA VEZ antes de agregar cualquier hoja
                CreateAndAddWorkbookStyles(workbookPart);

                // Agregar la parte de SharedStringTable
                var sharedStringTablePart = workbookPart.AddNewPart<SharedStringTablePart>();
                sharedStringTablePart.SharedStringTable = new SharedStringTable();

                // LUEGO: Crear hojas y aplicar estilos a cada una
                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var worksheetData = new SheetData();
                worksheetPart.Worksheet = new Worksheet(worksheetData);

                var userWorksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var userWorksheetData = new SheetData();
                userWorksheetPart.Worksheet = new Worksheet(userWorksheetData);

                var projectWorksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var projectWorksheetData = new SheetData();
                projectWorksheetPart.Worksheet = new Worksheet(projectWorksheetData);

                var instructionsWorksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var instructionsWorksheetData = new SheetData();
                instructionsWorksheetPart.Worksheet = new Worksheet(instructionsWorksheetData);

                var userListWorksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var userListWorksheetData = new SheetData();
                userListWorksheetPart.Worksheet = new Worksheet(userListWorksheetData);

                var captureSourcesWorksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var captureSourcesWorksheetData = new SheetData();
                captureSourcesWorksheetPart.Worksheet = new Worksheet(captureSourcesWorksheetData);

                var projectListWorksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var projectListWorksheetData = new SheetData();
                projectListWorksheetPart.Worksheet = new Worksheet(projectListWorksheetData);

                // Agregar definiciones de hojas al libro
                var sheets = spreadsheetDocument.WorkbookPart.Workbook.AppendChild(new Sheets());

                sheets.AppendChild(
                    new Sheet()
                    {
                        Id = spreadsheetDocument.WorkbookPart.GetIdOfPart(worksheetPart),
                        SheetId = 1,
                        Name = "Plantilla Clientes",
                    }
                );
                sheets.AppendChild(
                    new Sheet()
                    {
                        Id = spreadsheetDocument.WorkbookPart.GetIdOfPart(userWorksheetPart),
                        SheetId = 2,
                        Name = "Usuarios Disponibles",
                    }
                );
                sheets.AppendChild(
                    new Sheet()
                    {
                        Id = spreadsheetDocument.WorkbookPart.GetIdOfPart(projectWorksheetPart),
                        SheetId = 3,
                        Name = "Proyectos Disponibles",
                    }
                );
                sheets.AppendChild(
                    new Sheet()
                    {
                        Id = spreadsheetDocument.WorkbookPart.GetIdOfPart(
                            instructionsWorksheetPart
                        ),
                        SheetId = 4,
                        Name = "Instrucciones",
                    }
                );
                sheets.AppendChild(
                    new Sheet()
                    {
                        Id = spreadsheetDocument.WorkbookPart.GetIdOfPart(userListWorksheetPart),
                        SheetId = 5,
                        Name = "ListasOcultas",
                        State = SheetStateValues.Hidden,
                    }
                );
                sheets.AppendChild(
                    new Sheet()
                    {
                        Id = spreadsheetDocument.WorkbookPart.GetIdOfPart(
                            captureSourcesWorksheetPart
                        ),
                        SheetId = 6,
                        Name = "MediosCaptacion",
                        State = SheetStateValues.Hidden,
                    }
                );
                sheets.AppendChild(
                    new Sheet()
                    {
                        Id = spreadsheetDocument.WorkbookPart.GetIdOfPart(projectListWorksheetPart),
                        SheetId = 7,
                        Name = "ListaProyectos",
                        State = SheetStateValues.Hidden,
                    }
                );

                // Obtener la lista de usuarios
                var users = await _leadService.GetUsersSummaryAsync();

                // Obtener proyectos activos
                var projects = await _context.Projects.Where(p => p.IsActive).ToListAsync();

                // Crear una tabla de mapeo entre nombres de usuario e IDs en la hoja oculta
                // Agregar encabezados
                var userMappingHeaderRow = new Row { RowIndex = 1 };
                userListWorksheetData.AppendChild(userMappingHeaderRow);

                AddCellWithValue(
                    userMappingHeaderRow,
                    "A1",
                    "Nombre Usuario",
                    sharedStringTablePart
                );
                AddCellWithValue(userMappingHeaderRow, "B1", "ID Usuario", sharedStringTablePart);

                // Agregar usuarios a la hoja oculta
                int mappingRowIndex = 2;
                foreach (var user in users)
                {
                    var userRow = new Row { RowIndex = (uint)mappingRowIndex };
                    userListWorksheetData.AppendChild(userRow);

                    // El nombre del usuario va primero para la lista visual
                    AddCellWithValue(
                        userRow,
                        $"A{mappingRowIndex}",
                        user.UserName,
                        sharedStringTablePart
                    );

                    // El ID va en la segunda columna
                    AddCellWithValue(
                        userRow,
                        $"B{mappingRowIndex}",
                        user.Id.ToString(),
                        sharedStringTablePart
                    );

                    mappingRowIndex++;
                }

                // Crear la lista de medios de captación en la hoja oculta
                var captureSourceHeaderRow = new Row { RowIndex = 1 };
                captureSourcesWorksheetData.AppendChild(captureSourceHeaderRow);

                AddCellWithValue(
                    captureSourceHeaderRow,
                    "A1",
                    "Medio de Captación",
                    sharedStringTablePart
                );

                // Agregar los medios de captación traducidos a español
                var captureSourcesSpanish = new Dictionary<string, string>
                {
                    { "Company", "Empresa" },
                    { "PersonalFacebook", "FB Personal" },
                    { "RealEstateFair", "Feria inmobiliaria" },
                    { "Institutional", "Institucional" },
                    { "Loyalty", "Fidelizado" },
                };

                int captureSourceRowIndex = 2;
                foreach (var source in captureSourcesSpanish)
                {
                    var sourceRow = new Row { RowIndex = (uint)captureSourceRowIndex };
                    captureSourcesWorksheetData.AppendChild(sourceRow);

                    AddCellWithValue(
                        sourceRow,
                        $"A{captureSourceRowIndex}",
                        source.Value,
                        sharedStringTablePart
                    );

                    captureSourceRowIndex++;
                }

                // Crear la lista de proyectos en la hoja oculta
                var projectHeaderRow = new Row { RowIndex = 1 };
                projectListWorksheetData.AppendChild(projectHeaderRow);

                AddCellWithValue(projectHeaderRow, "A1", "Nombre Proyecto", sharedStringTablePart);
                AddCellWithValue(projectHeaderRow, "B1", "ID Proyecto", sharedStringTablePart);

                // Agregar proyectos a la hoja oculta
                int projectRowIndex = 2;
                foreach (var project in projects)
                {
                    var projectRow = new Row { RowIndex = (uint)projectRowIndex };
                    projectListWorksheetData.AppendChild(projectRow);

                    // El nombre del proyecto va primero para la lista visual
                    AddCellWithValue(
                        projectRow,
                        $"A{projectRowIndex}",
                        project.Name,
                        sharedStringTablePart
                    );

                    // El ID va en la segunda columna
                    AddCellWithValue(
                        projectRow,
                        $"B{projectRowIndex}",
                        project.Id.ToString(),
                        sharedStringTablePart
                    );

                    projectRowIndex++;
                }

                // Definir nombres para los rangos
                var definedNames = new DefinedNames();

                // Lista de usuarios
                var userListDefinedName = new DefinedName()
                {
                    Name = "UserList",
                    Text = $"'ListasOcultas'!$A$2:$A${mappingRowIndex - 1}",
                };
                definedNames.Append(userListDefinedName);

                // Lista de medios de captación
                var captureSourcesDefinedName = new DefinedName()
                {
                    Name = "CaptureSources",
                    Text = $"'MediosCaptacion'!$A$2:$A${captureSourceRowIndex - 1}",
                };
                definedNames.Append(captureSourcesDefinedName);

                // Lista de proyectos
                var projectListDefinedName = new DefinedName()
                {
                    Name = "ProjectList",
                    Text = $"'ListaProyectos'!$A$2:$A${projectRowIndex - 1}",
                };
                definedNames.Append(projectListDefinedName);

                workbookPart.Workbook.AppendChild(definedNames);

                // Configurar cada hoja utilizando métodos separados
                ConfigureMainSheet(worksheetPart, sharedStringTablePart, users);
                ConfigureUsersSheet(userWorksheetPart, sharedStringTablePart, users);
                ConfigureProjectsSheet(projectWorksheetPart, sharedStringTablePart, projects);
                ConfigureInstructionsSheet(instructionsWorksheetPart, sharedStringTablePart);

                // Configurar las validaciones de datos para las listas desplegables
                var dataValidations = new DataValidations();

                // Validación para Usuario Asignado (columna J)
                var userValidation = new DataValidation()
                {
                    Type = DataValidationValues.List,
                    AllowBlank = true,
                    Formula1 = new Formula1("UserList"),
                    ShowDropDown = false,
                };
                var userSqrefAttribute = new OpenXmlAttribute("sqref", "", "J2:J1000");
                userValidation.SetAttribute(userSqrefAttribute);
                dataValidations.Append(userValidation);

                // Validación para Medio de Captación (columna I)
                var captureSourceValidation = new DataValidation()
                {
                    Type = DataValidationValues.List,
                    AllowBlank = true,
                    Formula1 = new Formula1("CaptureSources"),
                    ShowDropDown = false,
                };
                var captureSourceSqrefAttribute = new OpenXmlAttribute("sqref", "", "I2:I1000");
                captureSourceValidation.SetAttribute(captureSourceSqrefAttribute);
                dataValidations.Append(captureSourceValidation);

                // Validación para Proyecto (columna K)
                var projectValidation = new DataValidation()
                {
                    Type = DataValidationValues.List,
                    AllowBlank = true,
                    Formula1 = new Formula1("ProjectList"),
                    ShowDropDown = false,
                };
                var projectSqrefAttribute = new OpenXmlAttribute("sqref", "", "K2:K1000");
                projectValidation.SetAttribute(projectSqrefAttribute);
                dataValidations.Append(projectValidation);

                // Añadir las validaciones a la hoja principal
                worksheetPart.Worksheet.AppendChild(dataValidations);

                // Guardar el libro de trabajo
                workbookPart.Workbook.Save();
            }

            // Reposicionar el stream para lectura
            stream.Position = 0;

            // Devolver el archivo como descarga con tipo MIME apropiado
            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "PlantillaImportacionClientes.xlsx"
            );
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error al generar la plantilla: {ex.Message}");
        }
    }

    private void ConfigureProjectsSheet(
        WorksheetPart projectWorksheetPart,
        SharedStringTablePart sharedStringTablePart,
        List<Project> projects
    )
    {
        var worksheetData = projectWorksheetPart.Worksheet.GetFirstChild<SheetData>();

        // 1. Título
        var titleRow = new Row
        {
            RowIndex = 1,
            Height = 25,
            CustomHeight = true,
        };
        worksheetData.AppendChild(titleRow);

        var titleCell = new Cell
        {
            CellReference = "A1",
            DataType = CellValues.SharedString,
            CellValue = new CellValue(
                AddSharedStringItem(sharedStringTablePart, "PROYECTOS DISPONIBLES").ToString()
            ),
            StyleIndex = 7, // Estilo amarillo con texto negro
        };
        titleRow.AppendChild(titleCell);

        var titleCell2 = new Cell
        {
            CellReference = "B1",
            DataType = CellValues.SharedString,
            CellValue = new CellValue(AddSharedStringItem(sharedStringTablePart, "").ToString()),
            StyleIndex = 7, // Estilo amarillo con texto negro
        };
        titleRow.AppendChild(titleCell2);

        // 2. Encabezados
        var headerRow = new Row
        {
            RowIndex = 2,
            Height = 20,
            CustomHeight = true,
        };
        worksheetData.AppendChild(headerRow);

        var idHeaderCell = new Cell
        {
            CellReference = "A2",
            DataType = CellValues.SharedString,
            CellValue = new CellValue(
                AddSharedStringItem(sharedStringTablePart, "ID PROYECTO").ToString()
            ),
            StyleIndex = 2, // Estilo de encabezado gris con texto blanco
        };
        headerRow.AppendChild(idHeaderCell);

        var nameHeaderCell = new Cell
        {
            CellReference = "B2",
            DataType = CellValues.SharedString,
            CellValue = new CellValue(
                AddSharedStringItem(sharedStringTablePart, "NOMBRE PROYECTO").ToString()
            ),
            StyleIndex = 2, // Estilo de encabezado gris con texto blanco
        };
        headerRow.AppendChild(nameHeaderCell);

        // 3. Datos
        int rowIndex = 3;
        foreach (var project in projects)
        {
            var dataRow = new Row { RowIndex = (uint)rowIndex };
            worksheetData.AppendChild(dataRow);

            // Alternar estilos para filas pares/impares
            uint styleIndex = (rowIndex % 2 == 0) ? 3U : 4U;

            var idCell = new Cell
            {
                CellReference = $"A{rowIndex}",
                DataType = CellValues.String,
                CellValue = new CellValue(project.Id.ToString()),
                StyleIndex = styleIndex,
            };
            dataRow.AppendChild(idCell);

            var nameCell = new Cell
            {
                CellReference = $"B{rowIndex}",
                DataType = CellValues.SharedString,
                CellValue = new CellValue(
                    AddSharedStringItem(sharedStringTablePart, project.Name).ToString()
                ),
                StyleIndex = styleIndex,
            };
            dataRow.AppendChild(nameCell);

            rowIndex++;
        }

        // 4. Anchos de columna
        var columns = new Columns();
        columns.Append(
            new Column
            {
                Min = 1,
                Max = 1,
                Width = 36,
                CustomWidth = true,
            }
        );
        columns.Append(
            new Column
            {
                Min = 2,
                Max = 2,
                Width = 25,
                CustomWidth = true,
            }
        );

        var existingColumns = projectWorksheetPart.Worksheet.GetFirstChild<Columns>();
        if (existingColumns != null)
            existingColumns.Remove();
        projectWorksheetPart.Worksheet.InsertAt(columns, 0);

        // 5. Fusionar celdas
        var mergeCells = new MergeCells();
        mergeCells.Append(new MergeCell { Reference = "A1:B1" });

        if (projectWorksheetPart.Worksheet.Elements<MergeCells>().Count() == 0)
        {
            projectWorksheetPart.Worksheet.AppendChild(mergeCells);
        }
        else
        {
            var existing = projectWorksheetPart.Worksheet.GetFirstChild<MergeCells>();
            existing.Remove();
            projectWorksheetPart.Worksheet.AppendChild(mergeCells);
        }
    }

    // Modificar ConfigureMainSheet para incluir LeadCaptureSource y Proyecto
    private void ConfigureMainSheet(
        WorksheetPart worksheetPart,
        SharedStringTablePart sharedStringTablePart,
        IEnumerable<UserSummaryDto> users
    )
    {
        var worksheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();

        // Definir encabezados para la hoja principal
        string[] headers = new string[]
        {
            "Nombre",
            "País",
            "DNI",
            "RUC",
            "Nombre Empresa",
            "Teléfono (+51)",
            "Email",
            "Dirección",
            "Medio de Captación", // Cambiado de "Procedencia" a "Medio de Captación"
            "Usuario Asignado",
            "Proyecto", // Nueva columna para proyectos
        };

        // Crear fila de encabezados
        var headerRow = new Row
        {
            RowIndex = 1,
            Height = 20,
            CustomHeight = true,
        };
        worksheetData.AppendChild(headerRow);

        // Agregar las celdas de encabezado con estilo
        for (var i = 0; i < headers.Length; i++)
        {
            var headerIndex = AddSharedStringItem(sharedStringTablePart, headers[i]);

            // Crear celda con el valor del encabezado
            var cell = new Cell
            {
                CellReference = GetColumnName(i + 1) + "1",
                DataType = CellValues.SharedString,
                CellValue = new CellValue(headerIndex.ToString()),
                StyleIndex = 2, // Estilo de encabezado (gris con texto blanco en negrita y bordes)
            };

            headerRow.AppendChild(cell);
        }

        // Ejemplo 1: Cliente natural con asignación
        var exampleRow1 = new Row { RowIndex = 2 };
        worksheetData.AppendChild(exampleRow1);

        AddCellWithValue(exampleRow1, "A2", "Juan Pérez", sharedStringTablePart);
        AddCellWithValue(exampleRow1, "B2", "Perú", sharedStringTablePart);
        AddCellWithValue(exampleRow1, "C2", "12345678", sharedStringTablePart);
        AddCellWithValue(exampleRow1, "F2", "+51999888777", sharedStringTablePart);
        AddCellWithValue(exampleRow1, "G2", "juan.perez@ejemplo.com", sharedStringTablePart);
        AddCellWithValue(exampleRow1, "H2", "Av. Principal 123", sharedStringTablePart);
        AddCellWithValue(exampleRow1, "I2", "Empresa", sharedStringTablePart); // Medio de Captación: Empresa

        // Mostrar el nombre del usuario por defecto
        var defaultUser = users.FirstOrDefault();
        if (defaultUser != null)
        {
            AddCellWithValue(exampleRow1, "J2", defaultUser.UserName, sharedStringTablePart);
        }

        // Ejemplo 2: Cliente jurídico sin asignación
        var exampleRow2 = new Row { RowIndex = 3 };
        worksheetData.AppendChild(exampleRow2);

        AddCellWithValue(exampleRow2, "A3", "Empresa ABC", sharedStringTablePart);

        // Crear celda de RUC como texto explícitamente para evitar problemas de formato
        var rucCell = new Cell
        {
            CellReference = "D3",
            DataType = CellValues.String,
            CellValue = new CellValue("20123456789"),
        };
        exampleRow2.AppendChild(rucCell);

        AddCellWithValue(exampleRow2, "E3", "ABC Corporación", sharedStringTablePart);
        AddCellWithValue(exampleRow2, "F3", "+51998877665", sharedStringTablePart);
        AddCellWithValue(exampleRow2, "G3", "contacto@empresaabc.com", sharedStringTablePart);
        AddCellWithValue(exampleRow2, "H3", "Jr. Comercial 456", sharedStringTablePart);
        AddCellWithValue(exampleRow2, "I3", "Feria inmobiliaria", sharedStringTablePart); // Medio de Captación: Feria inmobiliaria

        // Configurar anchos de columna adecuados
        var columns = new Columns();
        columns.Append(
            new Column
            {
                Min = 1,
                Max = 1,
                Width = 20,
                CustomWidth = true,
            }
        ); // Nombre
        columns.Append(
            new Column
            {
                Min = 2,
                Max = 2,
                Width = 20,
                CustomWidth = true,
            }
        ); // País
        columns.Append(
            new Column
            {
                Min = 3,
                Max = 3,
                Width = 12,
                CustomWidth = true,
            }
        ); // DNI
        columns.Append(
            new Column
            {
                Min = 4,
                Max = 4,
                Width = 15,
                CustomWidth = true,
            }
        ); // RUC
        columns.Append(
            new Column
            {
                Min = 5,
                Max = 5,
                Width = 25,
                CustomWidth = true,
            }
        ); // Nombre Empresa
        columns.Append(
            new Column
            {
                Min = 6,
                Max = 6,
                Width = 15,
                CustomWidth = true,
            }
        ); // Teléfono
        columns.Append(
            new Column
            {
                Min = 7,
                Max = 7,
                Width = 25,
                CustomWidth = true,
            }
        ); // Email
        columns.Append(
            new Column
            {
                Min = 8,
                Max = 8,
                Width = 30,
                CustomWidth = true,
            }
        ); // Dirección
        columns.Append(
            new Column
            {
                Min = 9,
                Max = 9,
                Width = 20,
                CustomWidth = true,
            }
        ); // Medio de Captación
        columns.Append(
            new Column
            {
                Min = 10,
                Max = 10,
                Width = 20,
                CustomWidth = true,
            }
        ); // Usuario Asignado
        columns.Append(
            new Column
            {
                Min = 11,
                Max = 11,
                Width = 25,
                CustomWidth = true,
            }
        ); // Proyecto

        var existingColumns = worksheetPart.Worksheet.GetFirstChild<Columns>();
        if (existingColumns != null)
            existingColumns.Remove();

        worksheetPart.Worksheet.InsertAt(columns, 0);
    }

    // Nuevo método para crear la hoja de estilos COMÚN para todo el libro
    private void CreateAndAddWorkbookStyles(WorkbookPart workbookPart)
    {
        var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet = new Stylesheet();

        // 1. Crear fuentes
        var fonts = new Fonts { Count = (UInt32Value)5U };

        // Fuente normal
        fonts.Append(
            new Font(
                new FontSize { Val = 11 },
                new Color { Rgb = new HexBinaryValue() { Value = "000000" } },
                new FontName { Val = "Calibri" }
            )
        );

        // Fuente negrita
        fonts.Append(
            new Font(
                new Bold(),
                new FontSize { Val = 11 },
                new Color { Rgb = new HexBinaryValue() { Value = "000000" } },
                new FontName { Val = "Calibri" }
            )
        );

        // Fuente negrita blanca
        fonts.Append(
            new Font(
                new Bold(),
                new FontSize { Val = 11 },
                new Color { Rgb = new HexBinaryValue() { Value = "FFFFFF" } },
                new FontName { Val = "Calibri" }
            )
        );

        // Fuente azul (para notas)
        fonts.Append(
            new Font(
                new Bold(),
                new FontSize { Val = 11 },
                new Color { Rgb = new HexBinaryValue() { Value = "0070C0" } },
                new FontName { Val = "Calibri" }
            )
        );

        // Fuente roja (para advertencias)
        fonts.Append(
            new Font(
                new Bold(),
                new FontSize { Val = 11 },
                new Color { Rgb = new HexBinaryValue() { Value = "FF0000" } },
                new FontName { Val = "Calibri" }
            )
        );

        // 2. Crear rellenos
        var fills = new Fills { Count = (UInt32Value)5U };

        // Rellenos estándar requeridos
        fills.Append(new Fill(new PatternFill { PatternType = PatternValues.None }));
        fills.Append(new Fill(new PatternFill { PatternType = PatternValues.Gray125 }));

        // Relleno amarillo (color principal)
        fills.Append(
            new Fill(
                new PatternFill
                {
                    PatternType = PatternValues.Solid,
                    ForegroundColor = new ForegroundColor
                    {
                        Rgb = new HexBinaryValue() { Value = "FFD038" },
                    },
                    BackgroundColor = new BackgroundColor { Indexed = (UInt32Value)64U },
                }
            )
        );

        // Relleno amarillo claro (filas alternadas)
        fills.Append(
            new Fill(
                new PatternFill
                {
                    PatternType = PatternValues.Solid,
                    ForegroundColor = new ForegroundColor
                    {
                        Rgb = new HexBinaryValue() { Value = "FFF1C9" },
                    },
                    BackgroundColor = new BackgroundColor { Indexed = (UInt32Value)64U },
                }
            )
        );

        // Relleno gris (para encabezados secundarios)
        fills.Append(
            new Fill(
                new PatternFill
                {
                    PatternType = PatternValues.Solid,
                    ForegroundColor = new ForegroundColor
                    {
                        Rgb = new HexBinaryValue() { Value = "393839" },
                    },
                    BackgroundColor = new BackgroundColor { Indexed = (UInt32Value)64U },
                }
            )
        );

        // 3. Crear bordes
        var borders = new Borders { Count = (UInt32Value)2U };

        // Sin bordes
        borders.Append(
            new Border(
                new LeftBorder(),
                new RightBorder(),
                new TopBorder(),
                new BottomBorder(),
                new DiagonalBorder()
            )
        );

        // Con bordes negros
        borders.Append(
            new Border(
                new LeftBorder
                {
                    Style = BorderStyleValues.Thin,
                    Color = new Color { Rgb = new HexBinaryValue { Value = "000000" } },
                },
                new RightBorder
                {
                    Style = BorderStyleValues.Thin,
                    Color = new Color { Rgb = new HexBinaryValue { Value = "000000" } },
                },
                new TopBorder
                {
                    Style = BorderStyleValues.Thin,
                    Color = new Color { Rgb = new HexBinaryValue { Value = "000000" } },
                },
                new BottomBorder
                {
                    Style = BorderStyleValues.Thin,
                    Color = new Color { Rgb = new HexBinaryValue { Value = "000000" } },
                },
                new DiagonalBorder()
            )
        );

        // 4. Crear formatos de celda
        var cellStyleFormats = new CellStyleFormats { Count = (UInt32Value)1U };
        cellStyleFormats.Append(
            new CellFormat
            {
                NumberFormatId = (UInt32Value)0U,
                FontId = (UInt32Value)0U,
                FillId = (UInt32Value)0U,
                BorderId = (UInt32Value)0U,
            }
        );

        var cellFormats = new CellFormats { Count = (UInt32Value)8U };

        // 0. Estilo por defecto
        cellFormats.Append(
            new CellFormat
            {
                NumberFormatId = (UInt32Value)0U,
                FontId = (UInt32Value)0U,
                FillId = (UInt32Value)0U,
                BorderId = (UInt32Value)0U,
                FormatId = (UInt32Value)0U,
            }
        );

        // 1. Título principal - fondo amarillo, negrita
        cellFormats.Append(
            new CellFormat
            {
                NumberFormatId = (UInt32Value)0U,
                FontId = (UInt32Value)1U,
                FillId = (UInt32Value)2U,
                BorderId = (UInt32Value)1U,
                FormatId = (UInt32Value)0U,
                Alignment = new Alignment
                {
                    Horizontal = HorizontalAlignmentValues.Center,
                    Vertical = VerticalAlignmentValues.Center,
                },
            }
        );

        // 2. Encabezados tabla - gris con texto blanco
        cellFormats.Append(
            new CellFormat
            {
                NumberFormatId = (UInt32Value)0U,
                FontId = (UInt32Value)2U,
                FillId = (UInt32Value)4U,
                BorderId = (UInt32Value)1U,
                FormatId = (UInt32Value)0U,
                Alignment = new Alignment
                {
                    Horizontal = HorizontalAlignmentValues.Center,
                    Vertical = VerticalAlignmentValues.Center,
                },
            }
        );

        // 3. Filas pares - fondo amarillo claro
        cellFormats.Append(
            new CellFormat
            {
                NumberFormatId = (UInt32Value)0U,
                FontId = (UInt32Value)0U,
                FillId = (UInt32Value)3U,
                BorderId = (UInt32Value)1U,
                FormatId = (UInt32Value)0U,
                Alignment = new Alignment { Vertical = VerticalAlignmentValues.Center },
            }
        );

        // 4. Filas impares - sin relleno
        cellFormats.Append(
            new CellFormat
            {
                NumberFormatId = (UInt32Value)0U,
                FontId = (UInt32Value)0U,
                FillId = (UInt32Value)0U,
                BorderId = (UInt32Value)1U,
                FormatId = (UInt32Value)0U,
                Alignment = new Alignment { Vertical = VerticalAlignmentValues.Center },
            }
        );

        // 5. Notas (azul)
        cellFormats.Append(
            new CellFormat
            {
                NumberFormatId = (UInt32Value)0U,
                FontId = (UInt32Value)3U,
                FillId = (UInt32Value)0U,
                BorderId = (UInt32Value)0U,
                FormatId = (UInt32Value)0U,
            }
        );

        // 6. Advertencias (rojo)
        cellFormats.Append(
            new CellFormat
            {
                NumberFormatId = (UInt32Value)0U,
                FontId = (UInt32Value)4U,
                FillId = (UInt32Value)0U,
                BorderId = (UInt32Value)0U,
                FormatId = (UInt32Value)0U,
            }
        );

        // 7. Encabezados amarillos con texto negro
        cellFormats.Append(
            new CellFormat
            {
                NumberFormatId = (UInt32Value)0U,
                FontId = (UInt32Value)1U,
                FillId = (UInt32Value)2U,
                BorderId = (UInt32Value)1U,
                FormatId = (UInt32Value)0U,
                Alignment = new Alignment
                {
                    Horizontal = HorizontalAlignmentValues.Center,
                    Vertical = VerticalAlignmentValues.Center,
                },
            }
        );

        // Añadir las partes a la hoja de estilos
        stylesPart.Stylesheet.Append(fonts);
        stylesPart.Stylesheet.Append(fills);
        stylesPart.Stylesheet.Append(borders);
        stylesPart.Stylesheet.Append(cellStyleFormats);
        stylesPart.Stylesheet.Append(cellFormats);

        // Guardar la hoja de estilos
        stylesPart.Stylesheet.Save();
    }

    private void ConfigureUsersSheet(
        WorksheetPart userWorksheetPart,
        SharedStringTablePart sharedStringTablePart,
        IEnumerable<UserSummaryDto> users
    )
    {
        var worksheetData = userWorksheetPart.Worksheet.GetFirstChild<SheetData>();

        // 1. Título
        var titleRow = new Row
        {
            RowIndex = 1,
            Height = 25,
            CustomHeight = true,
        };
        worksheetData.AppendChild(titleRow);

        var titleCell = new Cell
        {
            CellReference = "A1",
            DataType = CellValues.SharedString,
            CellValue = new CellValue(
                AddSharedStringItem(sharedStringTablePart, "USUARIOS DISPONIBLES").ToString()
            ),
            StyleIndex = 7, // Estilo amarillo con texto negro
        };
        titleRow.AppendChild(titleCell);

        var titleCell2 = new Cell
        {
            CellReference = "B1",
            DataType = CellValues.SharedString,
            CellValue = new CellValue(AddSharedStringItem(sharedStringTablePart, "").ToString()),
            StyleIndex = 7, // Estilo amarillo con texto negro
        };
        titleRow.AppendChild(titleCell2);

        // 2. Encabezados
        var headerRow = new Row
        {
            RowIndex = 2,
            Height = 20,
            CustomHeight = true,
        };
        worksheetData.AppendChild(headerRow);

        var idHeaderCell = new Cell
        {
            CellReference = "A2",
            DataType = CellValues.SharedString,
            CellValue = new CellValue(
                AddSharedStringItem(sharedStringTablePart, "ID USUARIO").ToString()
            ),
            StyleIndex = 2, // Estilo de encabezado gris con texto blanco
        };
        headerRow.AppendChild(idHeaderCell);

        var nameHeaderCell = new Cell
        {
            CellReference = "B2",
            DataType = CellValues.SharedString,
            CellValue = new CellValue(
                AddSharedStringItem(sharedStringTablePart, "NOMBRE USUARIO").ToString()
            ),
            StyleIndex = 2, // Estilo de encabezado gris con texto blanco
        };
        headerRow.AppendChild(nameHeaderCell);

        // 3. Datos
        int rowIndex = 3;
        foreach (var user in users)
        {
            var dataRow = new Row { RowIndex = (uint)rowIndex };
            worksheetData.AppendChild(dataRow);

            // Alternar estilos para filas pares/impares
            uint styleIndex = (rowIndex % 2 == 0) ? 3U : 4U;

            var idCell = new Cell
            {
                CellReference = $"A{rowIndex}",
                DataType = CellValues.String,
                CellValue = new CellValue(user.Id.ToString()),
                StyleIndex = styleIndex,
            };
            dataRow.AppendChild(idCell);

            var nameCell = new Cell
            {
                CellReference = $"B{rowIndex}",
                DataType = CellValues.SharedString,
                CellValue = new CellValue(
                    AddSharedStringItem(sharedStringTablePart, user.UserName).ToString()
                ),
                StyleIndex = styleIndex,
            };
            dataRow.AppendChild(nameCell);

            rowIndex++;
        }

        // 4. Anchos de columna
        var columns = new Columns();
        columns.Append(
            new Column
            {
                Min = 1,
                Max = 1,
                Width = 36,
                CustomWidth = true,
            }
        );
        columns.Append(
            new Column
            {
                Min = 2,
                Max = 2,
                Width = 25,
                CustomWidth = true,
            }
        );

        var existingColumns = userWorksheetPart.Worksheet.GetFirstChild<Columns>();
        if (existingColumns != null)
            existingColumns.Remove();
        userWorksheetPart.Worksheet.InsertAt(columns, 0);

        // 5. Fusionar celdas
        var mergeCells = new MergeCells();
        mergeCells.Append(new MergeCell { Reference = "A1:B1" });

        if (userWorksheetPart.Worksheet.Elements<MergeCells>().Count() == 0)
        {
            userWorksheetPart.Worksheet.AppendChild(mergeCells);
        }
        else
        {
            var existing = userWorksheetPart.Worksheet.GetFirstChild<MergeCells>();
            existing.Remove();
            userWorksheetPart.Worksheet.AppendChild(mergeCells);
        }
    }

    private void ConfigureInstructionsSheet(
        DocumentFormat.OpenXml.Packaging.WorksheetPart instructionsWorksheetPart,
        DocumentFormat.OpenXml.Packaging.SharedStringTablePart sharedStringTablePart
    )
    {
        var worksheetData = instructionsWorksheetPart.Worksheet.GetFirstChild<SheetData>();

        // 2. Agregar título principal
        var titleRow = new Row
        {
            RowIndex = 1,
            Height = 30,
            CustomHeight = true,
        };
        worksheetData.AppendChild(titleRow);

        var titleCell = new Cell
        {
            CellReference = "A1",
            StyleIndex = 1, // Usar estilo existente (título principal)
            DataType = CellValues.SharedString,
            CellValue = new CellValue(
                AddSharedStringItem(
                        sharedStringTablePart,
                        "INSTRUCCIONES PARA IMPORTAR CLIENTES Y ASIGNAR LEADS"
                    )
                    .ToString()
            ),
        };
        titleRow.AppendChild(titleCell);

        // 3. Agregar encabezados de la tabla de instrucciones con espacio
        var spacerRow = new Row { RowIndex = 2 };
        worksheetData.AppendChild(spacerRow);

        var headerRow = new Row
        {
            RowIndex = 3,
            Height = 20,
            CustomHeight = true,
        };
        worksheetData.AppendChild(headerRow);

        var headers = new[] { "CAMPO", "DESCRIPCIÓN", "OBLIGATORIO", "FORMATO / VALORES VÁLIDOS" };
        for (int i = 0; i < headers.Length; i++)
        {
            var headerCell = new Cell
            {
                CellReference = GetColumnName(i + 1) + "3",
                StyleIndex = 2, // Estilo de encabezado
                DataType = CellValues.SharedString,
                CellValue = new CellValue(
                    AddSharedStringItem(sharedStringTablePart, headers[i]).ToString()
                ),
            };
            headerRow.AppendChild(headerCell);
        }

        // 4. Agregar datos para la tabla de instrucciones
        string[][] instructionsData = new string[][]
        {
            new string[] { "Nombre", "Nombre completo del cliente", "No", "" },
            new string[] { "País", "País del cliente", "No", "" },
            new string[] { "DNI", "Número de DNI", "No", "8 dígitos, único pero no obligatorio" },
            new string[]
            {
                "RUC",
                "Número de RUC",
                "No*",
                "*Obligatorio para clientes jurídicos, 11 dígitos",
            },
            new string[]
            {
                "Nombre Empresa",
                "Nombre de la empresa",
                "No*",
                "*Solo para clientes jurídicos. Si está vacío, usa el campo Nombre",
            },
            new string[]
            {
                "Teléfono",
                "Número de teléfono",
                "Sí",
                "Debe ser único. Recomendado incluir código de país (51), se puede obviar el +",
            },
            new string[] { "Email", "Correo electrónico del cliente", "No", "formato@ejemplo.com" },
            new string[] { "Dirección", "Dirección completa del cliente", "No", "" },
            new string[]
            {
                "Medio de Captación",
                "Fuente por la que se captó el lead",
                "Si",
                "Seleccionar de la lista desplegable (Empresa, FB Personal, etc.)",
            },
            new string[]
            {
                "Usuario Asignado",
                "Usuario al que se asignará el lead",
                "No",
                "Seleccionar de la lista desplegable",
            },
            new string[]
            {
                "Proyecto",
                "Proyecto relacionado con el lead",
                "No",
                "Seleccionar de la lista desplegable",
            },
        };

        uint rowIndex = 4;
        foreach (var row in instructionsData)
        {
            var dataRow = new Row
            {
                RowIndex = rowIndex,
                Height = 18,
                CustomHeight = true,
            };
            worksheetData.AppendChild(dataRow);

            uint styleIndex = (rowIndex % 2 == 0) ? 3U : 4U; // Alternar estilos para filas pares/impares

            for (int i = 0; i < row.Length; i++)
            {
                var cell = new Cell
                {
                    CellReference = GetColumnName(i + 1) + rowIndex.ToString(),
                    StyleIndex = styleIndex,
                    DataType = CellValues.SharedString,
                    CellValue = new CellValue(
                        AddSharedStringItem(sharedStringTablePart, row[i]).ToString()
                    ),
                };
                dataRow.AppendChild(cell);
            }

            rowIndex++;
        }

        // 5. Espacio antes de la tabla de tipos de cliente
        rowIndex += 2;

        // 6. Tabla de tipos de cliente - Encabezado
        var clientTypesHeaderRow = new Row
        {
            RowIndex = rowIndex,
            Height = 25,
            CustomHeight = true,
        };
        worksheetData.AppendChild(clientTypesHeaderRow);

        var clientTypesHeaderCell = new Cell
        {
            CellReference = "A" + rowIndex.ToString(),
            StyleIndex = 1, // Estilo de título principal
            DataType = CellValues.SharedString,
            CellValue = new CellValue(
                AddSharedStringItem(sharedStringTablePart, "TIPOS DE CLIENTE").ToString()
            ),
        };
        clientTypesHeaderRow.AppendChild(clientTypesHeaderCell);

        // 7. Definir encabezados de la tabla de tipos
        rowIndex++;
        var typeTableHeaderRow = new Row
        {
            RowIndex = rowIndex,
            Height = 20,
            CustomHeight = true,
        };
        worksheetData.AppendChild(typeTableHeaderRow);

        var typeHeaders = new[] { "TIPO", "DESCRIPCIÓN", "CAMPOS REQUERIDOS", "OBSERVACIONES" };
        for (int i = 0; i < typeHeaders.Length; i++)
        {
            var headerCell = new Cell
            {
                CellReference = GetColumnName(i + 1) + rowIndex.ToString(),
                StyleIndex = 2, // Estilo de encabezado
                DataType = CellValues.SharedString,
                CellValue = new CellValue(
                    AddSharedStringItem(sharedStringTablePart, typeHeaders[i]).ToString()
                ),
            };
            typeTableHeaderRow.AppendChild(headerCell);
        }

        // 8. Datos de tipos de cliente
        rowIndex++;
        string[][] clientTypesData = new string[][]
        {
            new string[]
            {
                "Natural",
                "Persona individual",
                "Nombre, DNI",
                "DNI obligatorio, RUC y CompanyName deben estar vacíos",
            },
            new string[]
            {
                "Jurídico",
                "Empresa o persona jurídica",
                "Nombre, RUC",
                "RUC de 11 dígitos obligatorio, si CompanyName está vacío se usa el valor de Nombre",
            },
        };

        foreach (var row in clientTypesData)
        {
            var dataRow = new Row
            {
                RowIndex = rowIndex,
                Height = 18,
                CustomHeight = true,
            };
            worksheetData.AppendChild(dataRow);

            uint styleIndex = (rowIndex % 2 == 0) ? 3U : 4U; // Alternar estilos para filas pares/impares

            for (int i = 0; i < row.Length; i++)
            {
                var cell = new Cell
                {
                    CellReference = GetColumnName(i + 1) + rowIndex.ToString(),
                    StyleIndex = styleIndex,
                    DataType = CellValues.SharedString,
                    CellValue = new CellValue(
                        AddSharedStringItem(sharedStringTablePart, row[i]).ToString()
                    ),
                };
                dataRow.AppendChild(cell);
            }

            rowIndex++;
        }

        // 9. Espacio antes de las notas importantes
        rowIndex += 2;

        // 10. Notas adicionales
        string[][] notes = new string[][]
        {
            new string[]
            {
                "NOTA:",
                "Para asignar un usuario, selecciónelo de la lista desplegable en la columna 'Usuario Asignado'.",
            },
            new string[]
            {
                "IMPORTANTE:",
                "El teléfono es obligatorio y debe ser único. Si existe RUC, el cliente será jurídico, sino será cliente natural.",
            },
            new string[]
            {
                "RECOMENDACIÓN:",
                "Revise la hoja 'Usuarios Disponibles' para ver la lista completa de usuarios.",
            },
            new string[]
            {
                "⚠️ ADVERTENCIA:",
                "No modifique las listas desplegables ni elimine la hoja oculta 'ListasOcultas'.",
            },
        };

        foreach (var note in notes)
        {
            var noteRow = new Row { RowIndex = rowIndex };
            worksheetData.AppendChild(noteRow);

            // Determinar el estilo según el tipo de nota
            uint styleIndex = 5U; // Estilo normal azul para notas
            if (note[0].Contains("ADVERTENCIA"))
            {
                styleIndex = 6U; // Estilo rojo para advertencias
            }

            var labelCell = new Cell
            {
                CellReference = "A" + rowIndex.ToString(),
                StyleIndex = styleIndex,
                DataType = CellValues.SharedString,
                CellValue = new CellValue(
                    AddSharedStringItem(sharedStringTablePart, note[0]).ToString()
                ),
            };
            noteRow.AppendChild(labelCell);

            var textCell = new Cell
            {
                CellReference = "B" + rowIndex.ToString(),
                DataType = CellValues.SharedString,
                CellValue = new CellValue(
                    AddSharedStringItem(sharedStringTablePart, note[1]).ToString()
                ),
            };
            noteRow.AppendChild(textCell);

            rowIndex++;
        }

        // 11. Configuración de propiedades de columna para anchos ajustados
        var columns = new DocumentFormat.OpenXml.Spreadsheet.Columns();
        columns.Append(
            new Column()
            {
                Min = 1,
                Max = 1,
                Width = 20,
                CustomWidth = true,
            }
        );
        columns.Append(
            new Column()
            {
                Min = 2,
                Max = 2,
                Width = 30,
                CustomWidth = true,
            }
        );
        columns.Append(
            new Column()
            {
                Min = 3,
                Max = 3,
                Width = 15,
                CustomWidth = true,
            }
        );
        columns.Append(
            new Column()
            {
                Min = 4,
                Max = 4,
                Width = 40,
                CustomWidth = true,
            }
        );

        var existingColumns =
            instructionsWorksheetPart.Worksheet.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.Columns>();
        if (existingColumns != null)
        {
            existingColumns.Remove();
        }

        instructionsWorksheetPart.Worksheet.InsertAt(columns, 0);

        // 12. Fusionar celdas para títulos
        var mergeCells = new MergeCells();

        // Título principal
        mergeCells.Append(new MergeCell() { Reference = "A1:D1" });

        // Título de tipos de cliente
        mergeCells.Append(
            new MergeCell()
            {
                Reference = $"A{rowIndex - notes.Length - 2}:D{rowIndex - notes.Length - 2}",
            }
        );

        // Si ya existe una colección MergeCells, la reemplazamos
        if (
            instructionsWorksheetPart
                .Worksheet.Elements<DocumentFormat.OpenXml.Spreadsheet.MergeCells>()
                .Count() == 0
        )
        {
            instructionsWorksheetPart.Worksheet.AppendChild(mergeCells);
        }
        else
        {
            var existing =
                instructionsWorksheetPart.Worksheet.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.MergeCells>();
            existing.Remove();
            instructionsWorksheetPart.Worksheet.AppendChild(mergeCells);
        }
    }

    // Método para agregar un elemento a la tabla de cadenas compartidas y devolver su índice
    private int AddSharedStringItem(
        DocumentFormat.OpenXml.Packaging.SharedStringTablePart sharedStringTablePart,
        string text
    )
    {
        // Si la tabla de cadenas compartidas está vacía, agregar una
        if (sharedStringTablePart.SharedStringTable == null)
        {
            sharedStringTablePart.SharedStringTable =
                new DocumentFormat.OpenXml.Spreadsheet.SharedStringTable();
        }

        int i = 0;

        // Verificar si el texto ya existe en la tabla
        foreach (
            DocumentFormat.OpenXml.Spreadsheet.SharedStringItem item in sharedStringTablePart.SharedStringTable.Elements<DocumentFormat.OpenXml.Spreadsheet.SharedStringItem>()
        )
        {
            if (item.InnerText == text)
            {
                return i;
            }

            i++;
        }

        // El texto no existe, agregarlo
        sharedStringTablePart.SharedStringTable.AppendChild(
            new DocumentFormat.OpenXml.Spreadsheet.SharedStringItem(
                new DocumentFormat.OpenXml.Spreadsheet.Text(text)
            )
        );

        sharedStringTablePart.SharedStringTable.Save();

        return i;
    }

    // Método para agregar una celda con un valor específico
    private void AddCellWithValue(
        DocumentFormat.OpenXml.Spreadsheet.Row row,
        string cellReference,
        string text,
        DocumentFormat.OpenXml.Packaging.SharedStringTablePart sharedStringTablePart
    )
    {
        var index = AddSharedStringItem(sharedStringTablePart, text);

        var cell = new DocumentFormat.OpenXml.Spreadsheet.Cell
        {
            CellReference = cellReference,
            DataType = DocumentFormat.OpenXml.Spreadsheet.CellValues.SharedString,
            CellValue = new DocumentFormat.OpenXml.Spreadsheet.CellValue(index.ToString()),
        };

        row.AppendChild(cell);
    }

    // Método para obtener el nombre de columna (A, B, C, ...) desde un número
    private string GetColumnName(int columnIndex)
    {
        int dividend = columnIndex;
        string columnName = string.Empty;

        while (dividend > 0)
        {
            int modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar(65 + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }
}
