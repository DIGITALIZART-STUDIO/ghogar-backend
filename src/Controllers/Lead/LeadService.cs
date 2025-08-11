using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Services;

public class LeadService : ILeadService
{
    private readonly DatabaseContext _context;

    public LeadService(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateLeadCodeAsync()
    {
        // Formato: LEAD-YYYY-XXXXX donde YYYY es el año actual y XXXXX es un número secuencial
        int year = DateTime.UtcNow.Year;
        var yearPrefix = $"LEAD-{year}-";

        // Buscar el último código generado este año
        var lastLead = await _context
            .Leads.Where(l => l.Code.StartsWith(yearPrefix))
            .OrderByDescending(l => l.Code)
            .FirstOrDefaultAsync();

        int sequence = 1;
        if (lastLead != null)
        {
            var parts = lastLead.Code.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out int lastSequence))
            {
                sequence = lastSequence + 1;
            }
        }

        return $"{yearPrefix}{sequence:D5}";
    }

    public async Task<IEnumerable<Lead>> GetAllLeadsAsync()
    {
        return await _context
            .Leads.OrderByDescending(l => l.CreatedAt)
            .Include(l => l.Client)
            .Include(l => l.AssignedTo)
            .Include(l => l.Project)
            .ToListAsync();
    }

    public async Task<PaginatedResponseV2<Lead>> GetAllLeadsPaginatedAsync(
        int page,
        int pageSize,
        PaginationService paginationService
    )
    {
        var query = _context
            .Leads.OrderByDescending(l => l.CreatedAt)
            .Include(l => l.Client)
            .Include(l => l.AssignedTo)
            .Include(l => l.Project);

        return await paginationService.PaginateAsync(query, page, pageSize);
    }

    public async Task<Lead?> GetLeadByIdAsync(Guid id)
    {
        return await _context
            .Leads.Include(l => l.Client)
            .Include(l => l.AssignedTo)
            .Include(l => l.Project)
            .FirstOrDefaultAsync(l => l.Id == id && l.IsActive);
    }

    public async Task<Lead> CreateLeadAsync(Lead lead)
    {
        // Generar el código único para el lead
        lead.Code = await GenerateLeadCodeAsync();

        // Asegurarnos de que se establezcan las fechas correctamente
        lead.EntryDate = DateTime.UtcNow;
        lead.ExpirationDate = DateTime.UtcNow.AddDays(7);

        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();
        return lead;
    }

    public async Task<Lead?> UpdateLeadAsync(Guid id, Lead updatedLead)
    {
        var lead = await _context.Leads.FirstOrDefaultAsync(l => l.Id == id && l.IsActive);
        if (lead == null)
            return null;

        lead.ClientId = updatedLead.ClientId;
        lead.AssignedToId = updatedLead.AssignedToId;
        lead.Status = updatedLead.Status;
        lead.CaptureSource = updatedLead.CaptureSource;
        lead.ProjectId = updatedLead.ProjectId;
        lead.CompletionReason = updatedLead.CompletionReason;

        // Si se está cancelando, guardar el motivo
        if (
            updatedLead.Status == LeadStatus.Canceled
            && !string.IsNullOrEmpty(updatedLead.CancellationReason)
        )
        {
            lead.CancellationReason = updatedLead.CancellationReason;
        }

        lead.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return lead;
    }

    public async Task<Lead?> ChangeLeadStatusAsync(
        Guid id,
        LeadStatus status,
        LeadCompletionReason? reason
    )
    {
        var lead = await _context.Leads.FirstOrDefaultAsync(l => l.Id == id && l.IsActive);
        if (lead == null)
            return null;

        lead.Status = status;
        lead.ModifiedAt = DateTime.UtcNow;

        if (status == LeadStatus.Completed || status == LeadStatus.Canceled)
            lead.CompletionReason = reason;
        else
            lead.CompletionReason = null;

        await _context.SaveChangesAsync();
        return lead;
    }

    public async Task<bool> DeleteLeadAsync(Guid id)
    {
        var lead = await _context.Leads.FirstOrDefaultAsync(l => l.Id == id && l.IsActive);
        if (lead == null)
            return false;

        lead.IsActive = false; // El lead queda inactivo
        lead.Status = LeadStatus.Canceled; // Estado cancelado
        lead.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ActivateLeadAsync(Guid id)
    {
        var lead = await _context.Leads.FirstOrDefaultAsync(l => l.Id == id && !l.IsActive);
        if (lead == null)
            return false;

        lead.IsActive = true;

        // Si la fecha de expiración ya pasó, lo ponemos en Expired, si no, en Registered
        if (lead.ExpirationDate < DateTime.UtcNow)
            lead.Status = LeadStatus.Expired;
        else
            lead.Status = LeadStatus.Registered;

        lead.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<Lead>> GetInactiveLeadsAsync()
    {
        return await _context
            .Leads.Where(l => !l.IsActive)
            .Include(l => l.Client)
            .Include(l => l.AssignedTo)
            .Include(l => l.Project)
            .ToListAsync();
    }

    public async Task<IEnumerable<Lead>> GetLeadsByClientIdAsync(Guid clientId)
    {
        return await _context
            .Leads.Where(l => l.IsActive && l.ClientId == clientId)
            .OrderByDescending(l => l.CreatedAt)
            .Include(l => l.AssignedTo)
            .Include(l => l.Project)
            .ToListAsync();
    }

    public async Task<IEnumerable<Lead>> GetLeadsByAssignedToIdAsync(Guid userId)
    {
        return await _context
            .Leads.Where(l => l.IsActive && l.AssignedToId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .Include(l => l.Client)
            .Include(l => l.Project)
            .ToListAsync();
    }

    public async Task<PaginatedResponseV2<Lead>> GetLeadsByAssignedToIdPaginatedAsync(
        Guid userId,
        int page,
        int pageSize,
        PaginationService paginationService
    )
    {
        var query = _context
            .Leads.Where(l => l.IsActive && l.AssignedToId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .Include(l => l.Client)
            .Include(l => l.Project);

        return await paginationService.PaginateAsync(query, page, pageSize);
    }

    public async Task<IEnumerable<Lead>> GetLeadsByStatusAsync(LeadStatus status)
    {
        return await _context
            .Leads.Where(l => l.IsActive && l.Status == status)
            .Include(l => l.Client)
            .Include(l => l.AssignedTo)
            .Include(l => l.Project)
            .ToListAsync();
    }

    public async Task<IEnumerable<UserSummaryDto>> GetUsersSummaryAsync()
    {
        var salesAdvisorRoleId = await _context
            .Roles.Where(r => r.Name == "SalesAdvisor")
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        return await _context
            .Users.Where(u =>
                u.IsActive
                && _context.UserRoles.Any(ur =>
                    ur.UserId == u.Id && ur.RoleId == salesAdvisorRoleId
                )
            )
            .Select(u => new UserSummaryDto { Id = u.Id, UserName = u.Name })
            .ToListAsync();
    }

    /**
     * Método para obtener un resumen de los leads asignados a un usuario.
     * Incluye información del cliente y proyecto.
     */
    public async Task<IEnumerable<LeadSummaryDto>> GetAssignedLeadsSummaryAsync(Guid assignedToId)
    {
        var leads = await _context
            .Leads.Where(l => l.IsActive && l.AssignedToId == assignedToId)
            .Include(l => l.Client)
            .Include(l => l.Project)
            .ToListAsync();

        return leads.Select(LeadSummaryDto.FromEntity);
    }

    /**
     * Método para obtener leads disponibles para cotización de un usuario específico.
     * Excluye leads cancelados, expirados, completados o que ya tengan una cotización aceptada.
     */
    public async Task<IEnumerable<LeadSummaryDto>> GetAvailableLeadsForQuotationByUserAsync(
        Guid assignedToId,
        Guid? excludeQuotationId = null
    )
    {
        var leads = await _context
            .Leads.Where(l =>
                l.IsActive
                && l.AssignedToId == assignedToId
                && l.Status != LeadStatus.Canceled
                && l.Status != LeadStatus.Expired
                && l.Status != LeadStatus.Completed
                && !_context.Quotations.Any(q =>
                    q.LeadId == l.Id
                    && q.Status == QuotationStatus.ACCEPTED
                    && (!excludeQuotationId.HasValue || q.Id != excludeQuotationId.Value)
                )
            )
            .Include(l => l.Client)
            .Include(l => l.Project)
            .ToListAsync();

        return leads.Select(LeadSummaryDto.FromEntity);
    }

    // Nuevos métodos para manejar reciclaje y expiración

    public async Task<Lead?> RecycleLeadAsync(Guid id, Guid userId)
    {
        var lead = await _context.Leads.FirstOrDefaultAsync(l =>
            l.Id == id
            && l.IsActive
            && (l.Status == LeadStatus.Expired || l.Status == LeadStatus.Canceled)
        );

        if (lead == null)
            return null;

        lead.RecycleLead(userId);

        await _context.SaveChangesAsync();

        return lead;
    }

    public async Task<IEnumerable<Lead>> GetExpiredLeadsAsync()
    {
        return await _context
            .Leads.Where(l => l.IsActive && l.Status == LeadStatus.Expired)
            .Include(l => l.Client)
            .Include(l => l.AssignedTo)
            .Include(l => l.Project)
            .ToListAsync();
    }

    public async Task<int> CheckAndUpdateExpiredLeadsAsync()
    {
        var now = DateTime.UtcNow;

        // Buscar leads activos con fecha de expiración anterior a ahora y que no estén ya marcados como expirados
        var expiredLeads = await _context
            .Leads.Where(l =>
                l.IsActive
                && l.ExpirationDate < now
                && l.Status != LeadStatus.Expired
                && l.Status != LeadStatus.Completed
                && l.Status != LeadStatus.Canceled
            )
            .ToListAsync();

        foreach (var lead in expiredLeads)
        {
            lead.Status = LeadStatus.Expired;
            lead.ModifiedAt = now;
        }

        await _context.SaveChangesAsync();

        return expiredLeads.Count;
    }

    public async Task<byte[]> ExportLeadsToExcelAsync(IExcelExportService excelExportService)
    {
        var leads = await GetAllLeadsAsync();

        // Diccionarios para traducción
        var statusLabels = new Dictionary<string, string>
        {
            { "Registered", "Registrado" },
            { "Attended", "Atendido" },
            { "InFollowUp", "En seguimiento" },
            { "Completed", "Completado" },
            { "Canceled", "Cancelado" },
            { "Expired", "Expirado" },
        };
        var captureSourceLabels = new Dictionary<string, string>
        {
            { "Company", "Empresa" },
            { "PersonalFacebook", "Facebook personal" },
            { "RealEstateFair", "Feria inmobiliaria" },
            { "Institutional", "Institucional" },
            { "Loyalty", "Fidelización" },
        };
        var completionReasonLabels = new Dictionary<string, string>
        {
            { "NotInterested", "No interesado" },
            { "InFollowUp", "En seguimiento" },
            { "Sale", "Venta concretada" },
        };

        // Define los encabezados visibles
        var headers = new List<string>
        {
            "Código",
            "Cliente",
            "DNI",
            "Teléfono",
            "Email",
            "Dirección",
            "País",
            "Tipo",
            "CoPropietarios",
            "Propiedad Separada",
            "Asesor",
            "Estado",
            "Medio de Captación",
            "Proyecto",
            "Motivo de finalización",
        };

        var data = new List<List<object>>();

        foreach (var lead in leads)
        {
            var client = lead.Client;
            var coOwners = client?.CoOwners ?? "";
            var separatePropertyData = client?.SeparatePropertyData ?? "";

            // Traducción de estado, medio de captación y motivo de finalización
            var estado = statusLabels.ContainsKey(lead.Status.ToString())
                ? statusLabels[lead.Status.ToString()]
                : lead.Status.ToString();
            var medioCaptacion = captureSourceLabels.ContainsKey(lead.CaptureSource.ToString())
                ? captureSourceLabels[lead.CaptureSource.ToString()]
                : lead.CaptureSource.ToString();
            string motivoFinalizacion = "";
            if (lead.CompletionReason != null)
            {
                var reasonStr = lead.CompletionReason.ToString();
                motivoFinalizacion = completionReasonLabels.ContainsKey(reasonStr)
                    ? completionReasonLabels[reasonStr]
                    : reasonStr;
            }

            data.Add(
                new List<object>
                {
                    lead.Code,
                    client?.Name!,
                    client?.Dni!,
                    client?.PhoneNumber!,
                    client?.Email!,
                    client?.Address!,
                    client?.Country!,
                    client?.Type!,
                    coOwners,
                    separatePropertyData,
                    lead.AssignedTo?.Name!,
                    estado,
                    medioCaptacion,
                    lead.Project?.Name!,
                    motivoFinalizacion,
                }
            );
        }

        // Indica qué columnas son datos complejos (coOwners y separatePropertyData)
        var complexIndexes = new List<int> { 8, 9 };

        return excelExportService.GenerateExcel(
            "Reporte de Leads",
            headers,
            data,
            true,
            complexIndexes
        );
    }
}
