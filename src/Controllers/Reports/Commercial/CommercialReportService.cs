using GestionHogar.Controllers.Reports.Dto;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Services;

public class CommercialReportService : ICommercialReportService
{
    private readonly DatabaseContext _context;

    public CommercialReportService(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<PaginatedResponseV2<AdvisorActivityReportItemDto>> GetAdvisorActivityReportPaginatedAsync(
        DateOnly from,
        DateOnly to,
        int page,
        int pageSize,
        PaginationService paginationService,
        Guid currentUserId,
        IList<string> currentUserRoles,
        Guid? advisorId = null,
        LeadStatus[]? status = null,
        LeadCaptureSource[]? captureSource = null,
        bool? hasAdvisor = null,
        Guid? projectId = null,
        string? orderBy = null
    )
    {
        var leadsQuery = await BuildFilteredLeadsQueryAsync(
            from,
            to,
            currentUserId,
            currentUserRoles,
            advisorId,
            status,
            captureSource,
            hasAdvisor,
            projectId
        );

        var reportQuery = ProjectToReportItems(leadsQuery);
        reportQuery = ApplyOrdering(reportQuery, orderBy);

        return await paginationService.PaginateAsync(reportQuery, page, pageSize);
    }

    public async Task<List<AdvisorActivityTaskDto>> GetLeadTasksForReportAsync(
        Guid leadId,
        Guid currentUserId,
        IList<string> currentUserRoles
    )
    {
        var scopedLeads = ApplyRoleScope(_context.Leads.AsQueryable(), currentUserId, currentUserRoles);

        var leadExists = await scopedLeads.AnyAsync(l => l.Id == leadId);
        if (!leadExists)
            return new List<AdvisorActivityTaskDto>();

        return await _context
            .LeadTasks.Where(t => t.LeadId == leadId && t.IsActive)
            .OrderBy(t => t.ScheduledDate)
            .Select(t => new AdvisorActivityTaskDto
            {
                Id = t.Id,
                Type = t.Type,
                Description = t.Description,
                ScheduledDate = t.ScheduledDate,
                CompletedDate = t.CompletedDate,
                IsCompleted = t.IsCompleted,
            })
            .ToListAsync();
    }

    public async Task<byte[]> ExportAdvisorActivityReportAsync(
        DateOnly from,
        DateOnly to,
        IExcelExportService excelExportService,
        Guid currentUserId,
        IList<string> currentUserRoles,
        Guid? advisorId = null,
        LeadStatus[]? status = null,
        LeadCaptureSource[]? captureSource = null,
        bool? hasAdvisor = null,
        Guid? projectId = null
    )
    {
        var leadsQuery = await BuildFilteredLeadsQueryAsync(
            from,
            to,
            currentUserId,
            currentUserRoles,
            advisorId,
            status,
            captureSource,
            hasAdvisor,
            projectId
        );

        var leads = await leadsQuery
            .Include(l => l.Client)
            .Include(l => l.AssignedTo)
            .Include(l => l.Project)
            .OrderByDescending(l => l.EntryDate)
            .ToListAsync();

        var leadIds = leads.Select(l => l.Id).ToList();
        var tasksByLead = await _context
            .LeadTasks.Where(t => leadIds.Contains(t.LeadId) && t.IsActive)
            .OrderBy(t => t.ScheduledDate)
            .ToListAsync();
        var tasksLookup = tasksByLead.GroupBy(t => t.LeadId).ToDictionary(g => g.Key, g => g.ToList());

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
        var taskTypeLabels = new Dictionary<string, string>
        {
            { "Call", "Llamada" },
            { "Meeting", "Reunión" },
            { "Email", "Correo" },
            { "Visit", "Visita" },
            { "Other", "Otro" },
        };

        var headers = new List<string>
        {
            "Código",
            "Cliente",
            "Teléfono",
            "Asesor",
            "Estado",
            "Medio de Captación",
            "Proyecto",
            "Fecha de Ingreso",
            "Nº Tareas",
            "Última Fecha de Tarea",
            "Detalle de Tareas",
        };

        var data = new List<List<object>>();

        foreach (var lead in leads)
        {
            var leadTasks = tasksLookup.GetValueOrDefault(lead.Id, new List<LeadTask>());

            var estado = statusLabels.GetValueOrDefault(lead.Status.ToString(), lead.Status.ToString());
            var medioCaptacion = captureSourceLabels.GetValueOrDefault(
                lead.CaptureSource.ToString(),
                lead.CaptureSource.ToString()
            );

            var lastTaskDate = leadTasks
                .Select(t => (DateTime?)(t.CompletedDate ?? t.ScheduledDate))
                .OrderByDescending(d => d)
                .FirstOrDefault();

            var taskDetails = leadTasks
                .Select(t =>
                    $"{taskTypeLabels.GetValueOrDefault(t.Type.ToString(), t.Type.ToString())}: "
                    + $"Programada {t.ScheduledDate:dd/MM/yyyy} - "
                    + (t.IsCompleted
                        ? $"Completada {t.CompletedDate:dd/MM/yyyy}"
                        : "Pendiente")
                )
                .ToList();

            data.Add(
                new List<object>
                {
                    lead.Code,
                    lead.Client?.DisplayName ?? "",
                    lead.Client?.PhoneNumber ?? "",
                    lead.AssignedTo?.Name ?? "Sin asignar",
                    estado,
                    medioCaptacion,
                    lead.Project?.Name ?? "Sin proyecto",
                    lead.EntryDate.ToString("dd/MM/yyyy"),
                    leadTasks.Count,
                    lastTaskDate.HasValue ? lastTaskDate.Value.ToString("dd/MM/yyyy") : "Sin tareas",
                    taskDetails,
                }
            );
        }

        // Columna 10 (índice 10) contiene el detalle de tareas como lista -> se expande horizontalmente
        var complexIndexes = new List<int> { 10 };

        return excelExportService.GenerateExcel(
            "Reporte Comercial - Actividad de Asesores",
            headers,
            data,
            true,
            complexIndexes
        );
    }

    /// <summary>
    /// Construye la consulta base de Leads aplicando: rango de fechas (EntryDate),
    /// scoping por rol (Supervisor solo ve a sus asesores), y filtros opcionales.
    /// </summary>
    private async Task<IQueryable<Lead>> BuildFilteredLeadsQueryAsync(
        DateOnly from,
        DateOnly to,
        Guid currentUserId,
        IList<string> currentUserRoles,
        Guid? advisorId,
        LeadStatus[]? status,
        LeadCaptureSource[]? captureSource,
        bool? hasAdvisor,
        Guid? projectId
    )
    {
        var rangeStart = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var rangeEndExclusive = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var query = _context
            .Leads.Where(l => l.IsActive && l.EntryDate >= rangeStart && l.EntryDate < rangeEndExclusive)
            .AsQueryable();

        query = ApplyRoleScope(query, currentUserId, currentUserRoles);

        if (advisorId.HasValue)
        {
            query = query.Where(l => l.AssignedToId == advisorId.Value);
        }

        if (status != null && status.Length > 0)
        {
            query = query.Where(l => status.Contains(l.Status));
        }

        if (captureSource != null && captureSource.Length > 0)
        {
            query = query.Where(l => captureSource.Contains(l.CaptureSource));
        }

        if (hasAdvisor.HasValue)
        {
            query = hasAdvisor.Value
                ? query.Where(l => l.AssignedToId.HasValue)
                : query.Where(l => !l.AssignedToId.HasValue);
        }

        if (projectId.HasValue)
        {
            query = query.Where(l => l.ProjectId == projectId.Value);
        }

        // await ficticio para permitir futuras validaciones asíncronas (p.ej. de proyecto) sin cambiar la firma
        await Task.CompletedTask;

        return query;
    }

    /// <summary>
    /// Restringe la consulta de leads según el rol del usuario actual.
    /// Supervisor: solo leads asignados a sus SalesAdvisors (o a sí mismo).
    /// Admin/SuperAdmin/Manager/CommercialManager: sin restricción adicional (ven todos, incluyendo sin asignar).
    /// </summary>
    private IQueryable<Lead> ApplyRoleScope(
        IQueryable<Lead> query,
        Guid currentUserId,
        IList<string> currentUserRoles
    )
    {
        if (currentUserRoles.Contains("SuperAdmin") || currentUserRoles.Contains("Admin"))
        {
            return query;
        }

        if (currentUserRoles.Contains("Supervisor"))
        {
            var assignedSalesAdvisorIds = _context
                .SupervisorSalesAdvisors.Where(ssa => ssa.SupervisorId == currentUserId && ssa.IsActive)
                .Select(ssa => ssa.SalesAdvisorId);

            return query.Where(l =>
                l.AssignedToId.HasValue
                && (
                    assignedSalesAdvisorIds.Contains(l.AssignedToId.Value)
                    || l.AssignedToId.Value == currentUserId
                )
            );
        }

        // Manager / CommercialManager: sin restricción por equipo (ya limitado por proyecto si aplica)
        return query;
    }

    private IQueryable<AdvisorActivityReportItemDto> ProjectToReportItems(IQueryable<Lead> query)
    {
        return query.Select(l => new AdvisorActivityReportItemDto
        {
            LeadId = l.Id,
            LeadCode = l.Code,
            ClientId = l.ClientId,
            ClientName = l.Client != null ? (l.Client.Name ?? l.Client.CompanyName ?? "") : "",
            ClientPhone = l.Client != null ? l.Client.PhoneNumber : "",
            AssignedToId = l.AssignedToId,
            AssignedToName = l.AssignedTo != null ? l.AssignedTo.Name : null,
            Status = l.Status,
            CaptureSource = l.CaptureSource,
            EntryDate = l.EntryDate,
            ProjectId = l.ProjectId,
            ProjectName = l.Project != null ? l.Project.Name : null,
            TaskCount = _context.LeadTasks.Count(t => t.LeadId == l.Id && t.IsActive),
            LastTaskDate = _context
                .LeadTasks.Where(t => t.LeadId == l.Id && t.IsActive)
                .Select(t => (DateTime?)(t.CompletedDate ?? t.ScheduledDate))
                .OrderByDescending(d => d)
                .FirstOrDefault(),
        });
    }

    private static IQueryable<AdvisorActivityReportItemDto> ApplyOrdering(
        IQueryable<AdvisorActivityReportItemDto> query,
        string? orderBy
    )
    {
        if (string.IsNullOrWhiteSpace(orderBy))
        {
            return query.OrderByDescending(r => r.EntryDate);
        }

        var orderParts = orderBy.Split(' ');
        var field = orderParts[0].ToLower();
        var direction = orderParts.Length > 1 && orderParts[1].ToLower() == "desc" ? "desc" : "asc";

        return field switch
        {
            "leadcode" => direction == "desc"
                ? query.OrderByDescending(r => r.LeadCode)
                : query.OrderBy(r => r.LeadCode),
            "clientname" => direction == "desc"
                ? query.OrderByDescending(r => r.ClientName)
                : query.OrderBy(r => r.ClientName),
            "assignedtoname" => direction == "desc"
                ? query.OrderByDescending(r => r.AssignedToName)
                : query.OrderBy(r => r.AssignedToName),
            "status" => direction == "desc"
                ? query.OrderByDescending(r => r.Status)
                : query.OrderBy(r => r.Status),
            "capturesource" => direction == "desc"
                ? query.OrderByDescending(r => r.CaptureSource)
                : query.OrderBy(r => r.CaptureSource),
            "taskcount" => direction == "desc"
                ? query.OrderByDescending(r => r.TaskCount)
                : query.OrderBy(r => r.TaskCount),
            "lasttaskdate" => direction == "desc"
                ? query.OrderByDescending(r => r.LastTaskDate)
                : query.OrderBy(r => r.LastTaskDate),
            _ => query.OrderByDescending(r => r.EntryDate),
        };
    }
}
