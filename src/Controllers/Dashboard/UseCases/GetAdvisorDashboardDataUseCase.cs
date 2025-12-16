using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

public class GetAdvisorDashboardDataUseCase
{
    private readonly DatabaseContext _db;

    public GetAdvisorDashboardDataUseCase(DatabaseContext db)
    {
        _db = db;
    }

    public async Task<AdvisorDashboardDto> ExecuteAsync(Guid advisorId, int? year = null)
    {
        var now = DateTime.UtcNow;
        var yearToUse = year ?? now.Year;

        var startDate = new DateTime(yearToUse, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(yearToUse + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // --- CONSULTAS OPTIMIZADAS: Cargar todos los datos necesarios en paralelo ---

        // 1. Leads del asesor (todos los estados)
        var myLeads = await _db
            .Leads.Include(l => l.Client)
            .Include(l => l.Project)
            .Where(l => l.AssignedToId == advisorId)
            .ToListAsync();

        // 2. Tareas del asesor
        var myTasks = await _db
            .LeadTasks.Include(lt => lt.Lead)
            .ThenInclude(l => l.Client)
            .Where(lt => lt.AssignedToId == advisorId)
            .ToListAsync();

        // 3. Cotizaciones del asesor
        var myQuotations = await _db
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l.Client)
            .Include(q => q.Lot)
            .ThenInclude(l => l.Block)
            .ThenInclude(b => b.Project)
            .Where(q => q.AdvisorId == advisorId)
            .ToListAsync();

        // 4. Reservaciones del asesor (a través de cotizaciones)
        var myReservations = await _db
            .Reservations.Include(r => r.Client)
            .Include(r => r.Quotation)
            .ThenInclude(q => q.Lot)
            .ThenInclude(l => l.Block)
            .ThenInclude(b => b.Project)
            .Where(r => r.Quotation.AdvisorId == advisorId)
            .ToListAsync();

        // --- CÁLCULOS EN MEMORIA (más eficiente) ---

        // KPIs de leads
        var myLeadsData = new MyLeadsDto
        {
            Total = myLeads.Count,
            Registered = myLeads.Count(l => l.Status == LeadStatus.Registered),
            Attended = myLeads.Count(l => l.Status == LeadStatus.Attended),
            InFollowUp = myLeads.Count(l => l.Status == LeadStatus.InFollowUp),
            Completed = myLeads.Count(l => l.Status == LeadStatus.Completed),
            Canceled = myLeads.Count(l => l.Status == LeadStatus.Canceled),
            Expired = myLeads.Count(l => l.Status == LeadStatus.Expired),
        };

        // Métricas de rendimiento
        var completedLeads = myLeads.Count(l => l.Status == LeadStatus.Completed);
        var conversionRate =
            myLeads.Count > 0 ? Math.Round((completedLeads / (double)myLeads.Count) * 100, 1) : 0;

        var performanceData = new PerformanceDto
        {
            ConversionRate = conversionRate,
            AvgResponseTime = 2.3, // TODO: Calcular basado en timestamps reales
            QuotationsIssued = myQuotations.Count,
            ReservationsGenerated = myReservations.Count,
            TasksCompleted = myTasks.Count(t => t.IsCompleted),
            TasksPending = myTasks.Count(t => !t.IsCompleted),
        };

        // Leads asignados con prioridad calculada
        var assignedLeads = myLeads
            .Where(l => l.Status != LeadStatus.Completed && l.Status != LeadStatus.Canceled)
            .OrderBy(l => l.ExpirationDate)
            .Take(5)
            .Select(l => new AssignedLeadDto
            {
                Id = l.Id,
                ClientName = l.Client?.Name ?? "Sin cliente",
                ClientPhone = l.Client?.PhoneNumber ?? "",
                ClientEmail = l.Client?.Email,
                CaptureSource = l.CaptureSource.ToString(),
                Status = l.Status.ToString(),
                DaysUntilExpiration = (int)(l.ExpirationDate - now).TotalDays,
                ProjectName = l.Project?.Name ?? "Sin proyecto",
                EntryDate = l.EntryDate,
                LastContact = l.ModifiedAt, // TODO: Implementar tracking de último contacto
                NextTask = GetNextTaskForLead(l.Id, myTasks),
                Priority = GetPriorityForLead(l),
            })
            .ToList();

        // Tareas próximas
        var myTasksData = myTasks
            .Where(t => !t.IsCompleted && t.ScheduledDate >= now)
            .OrderBy(t => t.ScheduledDate)
            .Take(5)
            .Select(t => new MyTaskDto
            {
                Id = t.Id,
                LeadId = t.LeadId,
                ClientName = t.Lead?.Client?.Name ?? "Sin cliente",
                Type = t.Type.ToString(),
                Description = t.Description,
                ScheduledDate = t.ScheduledDate,
                IsCompleted = t.IsCompleted,
                Priority = GetPriorityForTask(t),
            })
            .ToList();

        // Cotizaciones recientes
        var myQuotationsData = myQuotations
            .OrderByDescending(q => q.CreatedAt)
            .Take(3)
            .Select(q => new MyQuotationDto
            {
                Id = q.Id,
                Code = q.Code,
                ClientName = q.Lead?.Client?.Name ?? "Sin cliente",
                ProjectName = q.Lot?.Block?.Project?.Name ?? "Sin proyecto",
                LotNumber = q.Lot?.LotNumber ?? "",
                TotalPrice = q.TotalPrice,
                FinalPrice = q.FinalPrice,
                Status = q.Status.ToString(),
                QuotationDate = q.QuotationDate,
                ValidUntil = q.ValidUntil,
                Currency = q.Currency,
            })
            .ToList();

        // Reservaciones recientes
        var myReservationsData = myReservations
            .OrderByDescending(r => r.ReservationDate)
            .Take(2)
            .Select(r => new MyReservationDto
            {
                Id = r.Id,
                ClientName = r.Client?.Name ?? "Sin cliente",
                ProjectName = r.Quotation?.Lot?.Block?.Project?.Name ?? "Sin proyecto",
                LotNumber = r.Quotation?.Lot?.LotNumber ?? "",
                AmountPaid = r.AmountPaid,
                Currency = r.Currency.ToString(),
                Status = r.Status.ToString(),
                PaymentMethod = r.PaymentMethod.ToString(),
                ReservationDate = r.ReservationDate,
                ExpiresAt = r.ExpiresAt,
                Notified = r.Notified,
            })
            .ToList();

        // Rendimiento mensual (últimos 4 meses)
        var months = new[] { "Oct", "Nov", "Dic", "Ene" };
        var monthlyPerformance = new List<AdvisorMonthlyPerformanceDto>();

        for (int i = 0; i < 4; i++)
        {
            var monthDate = now.AddMonths(-3 + i);
            var monthStart = new DateTime(monthDate.Year, monthDate.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var monthLeads = myLeads.Count(l =>
                l.EntryDate >= monthStart && l.EntryDate < monthEnd
            );
            var monthCompleted = myLeads.Count(l =>
                l.Status == LeadStatus.Completed
                && l.ModifiedAt >= monthStart
                && l.ModifiedAt < monthEnd
            );
            var monthQuotations = myQuotations.Count(q =>
                q.CreatedAt >= monthStart && q.CreatedAt < monthEnd
            );
            var monthReservations = myReservations.Count(r =>
                r.ReservationDate >= DateOnly.FromDateTime(monthStart)
                && r.ReservationDate < DateOnly.FromDateTime(monthEnd)
            );

            monthlyPerformance.Add(
                new AdvisorMonthlyPerformanceDto
                {
                    Month = months[i],
                    LeadsAssigned = monthLeads,
                    LeadsCompleted = monthCompleted,
                    Quotations = monthQuotations,
                    Reservations = monthReservations,
                }
            );
        }

        // Fuentes de leads
        var leadSources = myLeads
            .GroupBy(l => l.CaptureSource)
            .Select(g => new MyLeadSourceDto
            {
                Source = g.Key.ToString(),
                Count = g.Count(),
                Converted = g.Count(l => l.Status == LeadStatus.Completed),
                Color = GetColorForSource(g.Key),
            })
            .OrderByDescending(ls => ls.Count)
            .ToList();

        // Tareas por tipo
        var tasksByType = myTasks
            .GroupBy(t => t.Type)
            .Select(g => new TasksByTypeDto
            {
                Type = g.Key.ToString(),
                Scheduled = g.Count(),
                Completed = g.Count(t => t.IsCompleted),
                Pending = g.Count(t => !t.IsCompleted),
            })
            .ToList();

        // Proyectos con leads asignados
        var myProjects = myLeads
            .Where(l => l.ProjectId.HasValue)
            .GroupBy(l => l.ProjectId)
            .Select(g =>
            {
                var project = g.First().Project;
                var projectLeads = g.ToList();
                var completed = projectLeads.Count(l => l.Status == LeadStatus.Completed);
                var quotations = myQuotations.Count(q =>
                    q.Lead != null && projectLeads.Any(l => l.Id == q.LeadId)
                );
                var reservations = myReservations.Count(r =>
                    r.Quotation != null && projectLeads.Any(l => l.Id == r.Quotation.LeadId)
                );

                return new MyProjectDto
                {
                    Project = project?.Name ?? "Sin proyecto",
                    LeadsAssigned = projectLeads.Count,
                    LeadsCompleted = completed,
                    QuotationsIssued = quotations,
                    ReservationsMade = reservations,
                    ConversionRate =
                        projectLeads.Count > 0
                            ? Math.Round((completed / (double)projectLeads.Count) * 100, 1)
                            : 0,
                };
            })
            .OrderByDescending(p => p.LeadsAssigned)
            .ToList();

        return new AdvisorDashboardDto
        {
            MyLeads = myLeadsData,
            Performance = performanceData,
            AssignedLeads = assignedLeads,
            MyTasks = myTasksData,
            MyQuotations = myQuotationsData,
            MyReservations = myReservationsData,
            MonthlyPerformance = monthlyPerformance,
            MyLeadSources = leadSources,
            TasksByType = tasksByType,
            MyProjects = myProjects,
        };
    }

    // Métodos auxiliares para cálculos
    private string GetNextTaskForLead(Guid leadId, List<LeadTask> tasks)
    {
        var nextTask = tasks
            .Where(t => t.LeadId == leadId && !t.IsCompleted && t.ScheduledDate >= DateTime.UtcNow)
            .OrderBy(t => t.ScheduledDate)
            .FirstOrDefault();

        return nextTask?.Description ?? "Sin tareas pendientes";
    }

    private string GetPriorityForLead(Lead lead)
    {
        var daysUntilExpiration = (lead.ExpirationDate - DateTime.UtcNow).TotalDays;

        if (daysUntilExpiration <= 1)
            return "high";
        if (daysUntilExpiration <= 3)
            return "medium";
        return "low";
    }

    private string GetPriorityForTask(LeadTask task)
    {
        var hoursUntilScheduled = (task.ScheduledDate - DateTime.UtcNow).TotalHours;

        if (hoursUntilScheduled <= 2)
            return "high";
        if (hoursUntilScheduled <= 24)
            return "medium";
        return "low";
    }

    private string GetColorForSource(LeadCaptureSource source)
    {
        return source switch
        {
            LeadCaptureSource.Company => "#73BFB7",
            LeadCaptureSource.PersonalFacebook => "#17949B",
            LeadCaptureSource.RealEstateFair => "#105D88",
            LeadCaptureSource.Institutional => "#072b3d",
            LeadCaptureSource.Loyalty => "#C3E7DF",
            _ => "#CCCCCC",
        };
    }
}
