using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GestionHogar.Services;

public class LandingService : ILandingService
{
    private readonly DatabaseContext _context;
    private readonly ILogger<LandingService> _logger;
    private readonly ILeadService _leadService;

    public LandingService(
        DatabaseContext context,
        ILogger<LandingService> logger,
        ILeadService leadService
    )
    {
        _context = context;
        _logger = logger;
        _leadService = leadService;
    }

    public async Task<ReferralResultDto> ProcessReferralFromLandingAsync(
        ReferralCreateDto referralDto
    )
    {
        var result = new ReferralResultDto { ProcessInfo = new ReferralProcessInfo() };

        try
        {
            // 1. Buscar o crear el cliente referidor
            var referrerClient = await FindOrCreateReferrerClientAsync(
                referralDto.Referrer,
                result.ProcessInfo
            );
            result.ReferrerClientId = referrerClient.Id;

            // 2. Buscar o crear el cliente referenciado
            var referredClient = await FindOrCreateReferredClientAsync(
                referralDto.Referred,
                result.ProcessInfo
            );

            // 3. Crear el lead del referenciado (siempre se crea un nuevo lead)
            var referredLead = await CreateLeadForReferredClientAsync(referredClient);
            result.ReferredLeadId = referredLead.Id;
            result.ReferredLeadCode = referredLead.Code;
            result.ProcessInfo.ReferredLeadCreated = true;

            // 4. Crear el referral
            var referral = new Referral
            {
                ReferrerClientId = referrerClient.Id,
                ReferredLeadId = referredLead.Id,
            };

            _context.Referrals.Add(referral);
            await _context.SaveChangesAsync();

            result.ReferralId = referral.Id;
            result.ProcessInfo.ReferralCreated = true;

            // 5. Generar mensaje de éxito
            result.Message = GenerateSuccessMessage(result.ProcessInfo);

            _logger.LogInformation(
                "Referral procesado exitosamente. ReferralId: {ReferralId}, ReferrerClientId: {ReferrerClientId}, ReferredLeadId: {ReferredLeadId}",
                result.ReferralId,
                result.ReferrerClientId,
                result.ReferredLeadId
            );

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al procesar referral desde landing");
            throw;
        }
    }

    private async Task<Client> FindOrCreateReferrerClientAsync(
        ReferrerDataDto clientData,
        ReferralProcessInfo processInfo
    )
    {
        // Buscar por teléfono (campo único y obligatorio)
        var existingClient = await _context.Clients.FirstOrDefaultAsync(c =>
            c.PhoneNumber == clientData.Telefono && c.IsActive
        );

        if (existingClient != null)
        {
            processInfo.ReferrerClientExisted = true;
            return existingClient;
        }

        // Crear nuevo cliente
        var fullName = $"{clientData.Nombres} {clientData.Apellidos}".Trim();
        var newClient = new Client
        {
            Name = fullName,
            PhoneNumber = clientData.Telefono,
            Email = clientData.Email,
            Dni = clientData.NumeroDocumento.Length == 8 ? clientData.NumeroDocumento : null,
            Ruc = clientData.NumeroDocumento.Length == 11 ? clientData.NumeroDocumento : null,
            Type =
                clientData.NumeroDocumento.Length == 8 ? ClientType.Natural : ClientType.Juridico,
        };

        _context.Clients.Add(newClient);
        await _context.SaveChangesAsync();

        processInfo.ReferrerClientCreated = true;
        return newClient;
    }

    private async Task<Client> FindOrCreateReferredClientAsync(
        ReferredDataDto clientData,
        ReferralProcessInfo processInfo
    )
    {
        // Buscar por teléfono (campo único y obligatorio)
        var existingClient = await _context.Clients.FirstOrDefaultAsync(c =>
            c.PhoneNumber == clientData.Telefono && c.IsActive
        );

        if (existingClient != null)
        {
            processInfo.ReferredClientExisted = true;
            return existingClient;
        }

        // Crear nuevo cliente
        var fullName = $"{clientData.Nombres} {clientData.Apellidos}".Trim();
        var newClient = new Client
        {
            Name = fullName,
            PhoneNumber = clientData.Telefono,
            Email = clientData.Email,
            Dni = clientData.NumeroDocumento.Length == 8 ? clientData.NumeroDocumento : null,
            Ruc = clientData.NumeroDocumento.Length == 11 ? clientData.NumeroDocumento : null,
            Type =
                clientData.NumeroDocumento.Length == 8 ? ClientType.Natural : ClientType.Juridico,
        };

        _context.Clients.Add(newClient);
        await _context.SaveChangesAsync();

        processInfo.ReferredClientCreated = true;
        return newClient;
    }

    private async Task<Lead> CreateLeadForReferredClientAsync(Client referredClient)
    {
        // Usar el servicio existente para generar el código
        var leadCode = await _leadService.GenerateLeadCodeAsync();

        var lead = new Lead
        {
            Code = leadCode,
            ClientId = referredClient.Id,
            CaptureSource = LeadCaptureSource.Loyalty, // Referido = fidelizado
        };

        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();

        return lead;
    }

    private string GenerateSuccessMessage(ReferralProcessInfo processInfo)
    {
        var messages = new List<string>();

        if (processInfo.ReferrerClientCreated)
            messages.Add("Cliente referidor creado");
        else if (processInfo.ReferrerClientExisted)
            messages.Add("Cliente referidor encontrado");

        if (processInfo.ReferredClientCreated)
            messages.Add("Cliente referenciado creado");
        else if (processInfo.ReferredClientExisted)
            messages.Add("Cliente referenciado encontrado");

        messages.Add("Lead creado");
        messages.Add("Referencia registrada");

        return $"Proceso completado: {string.Join(", ", messages)}";
    }

    public async Task<ContactResultDto> ProcessContactFromLandingAsync(ContactCreateDto contactDto)
    {
        var result = new ContactResultDto
        {
            ProjectId = contactDto.ProjectId,
            ProcessInfo = new ContactProcessInfo(),
        };

        try
        {
            // 1. Buscar o crear el cliente
            var client = await FindOrCreateClientForContactAsync(contactDto, result.ProcessInfo);
            result.ClientId = client.Id;

            // 2. Crear el lead del cliente
            var lead = await CreateLeadForContactClientAsync(client, contactDto.ProjectId);
            result.LeadId = lead.Id;
            result.LeadCode = lead.Code;
            result.ProcessInfo.LeadCreated = true;

            // 3. Obtener información del proyecto
            var project = await _context.Projects.FirstOrDefaultAsync(p =>
                p.Id == contactDto.ProjectId
            );
            result.ProjectName = project?.Name ?? "Proyecto no encontrado";

            // 4. Generar mensaje de éxito
            result.Message = GenerateContactSuccessMessage(result.ProcessInfo);

            _logger.LogInformation(
                "Contacto procesado exitosamente desde landing. ClientId: {ClientId}, LeadId: {LeadId}, ProjectId: {ProjectId}",
                result.ClientId,
                result.LeadId,
                result.ProjectId
            );

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al procesar contacto desde landing");
            throw;
        }
    }

    private async Task<Client> FindOrCreateClientForContactAsync(
        ContactCreateDto contactDto,
        ContactProcessInfo processInfo
    )
    {
        // Buscar por teléfono (campo único y obligatorio)
        var existingClient = await _context.Clients.FirstOrDefaultAsync(c =>
            c.PhoneNumber == contactDto.Telefono && c.IsActive
        );

        if (existingClient != null)
        {
            processInfo.ClientExisted = true;
            return existingClient;
        }

        // Crear nuevo cliente
        var fullName = $"{contactDto.Nombres} {contactDto.Apellidos}".Trim();
        var newClient = new Client
        {
            Name = fullName,
            PhoneNumber = contactDto.Telefono,
            Email = contactDto.Email,
            Dni = contactDto.NumeroDocumento.Length == 8 ? contactDto.NumeroDocumento : null,
            Ruc = contactDto.NumeroDocumento.Length == 11 ? contactDto.NumeroDocumento : null,
            Type =
                contactDto.NumeroDocumento.Length == 8 ? ClientType.Natural : ClientType.Juridico,
        };

        _context.Clients.Add(newClient);
        await _context.SaveChangesAsync();

        processInfo.ClientCreated = true;
        return newClient;
    }

    private async Task<Lead> CreateLeadForContactClientAsync(Client client, Guid projectId)
    {
        // Usar el servicio existente para generar el código
        var leadCode = await _leadService.GenerateLeadCodeAsync();

        var lead = new Lead
        {
            Code = leadCode,
            ClientId = client.Id,
            CaptureSource = LeadCaptureSource.Company, // Contacto = empresa
            ProjectId = projectId,
        };

        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();

        return lead;
    }

    private string GenerateContactSuccessMessage(ContactProcessInfo processInfo)
    {
        var messages = new List<string>();

        if (processInfo.ClientCreated)
            messages.Add("Cliente creado");
        else if (processInfo.ClientExisted)
            messages.Add("Cliente encontrado");

        messages.Add("Lead creado");

        return $"Proceso completado: {string.Join(", ", messages)}";
    }

    public async Task<IEnumerable<Project>> GetActiveProjectsAsync()
    {
        return await _context.Projects.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
    }
}
