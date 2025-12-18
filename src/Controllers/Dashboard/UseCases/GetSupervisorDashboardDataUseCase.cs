using GestionHogar.Controllers;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

public class GetSupervisorDashboardDataUseCase
{
    private readonly DatabaseContext _db;

    public GetSupervisorDashboardDataUseCase(DatabaseContext db)
    {
        _db = db;
    }

    public async Task<SupervisorDashboardDto> ExecuteAsync(Guid supervisorId, int? year = null)
    {
        var now = DateTime.UtcNow;
        var yearToUse = year ?? now.Year;

        var startDate = new DateTime(yearToUse, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(yearToUse + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // --- OBTENER ASESORES ASIGNADOS AL SUPERVISOR ---
        var assignedAdvisorIds = await _db
            .SupervisorSalesAdvisors.Where(ssa => ssa.SupervisorId == supervisorId && ssa.IsActive)
            .Select(ssa => ssa.SalesAdvisorId)
            .ToListAsync();

        // Si no hay asesores asignados, retornar dashboard vacío
        if (!assignedAdvisorIds.Any())
        {
            return new SupervisorDashboardDto();
        }

        // --- CONSULTAS OPTIMIZADAS: Cargar todos los datos necesarios FILTRADOS POR ASESORES ASIGNADOS ---

        // 1. Cargar leads del año asignados a los asesores del supervisor
        var allLeads = await _db
            .Leads.Include(l => l.Client)
            .Include(l => l.AssignedTo)
            .Include(l => l.Project)
            .Where(l =>
                l.EntryDate >= startDate
                && l.EntryDate < endDate
                && l.AssignedToId.HasValue
                && assignedAdvisorIds.Contains(l.AssignedToId!.Value)
            )
            .ToListAsync();

        // 2. Cargar tareas del año asignadas a los asesores del supervisor
        var allTasks = await _db
            .LeadTasks.Include(lt => lt.AssignedTo)
            .Where(lt =>
                lt.CreatedAt >= startDate
                && lt.CreatedAt < endDate
                && assignedAdvisorIds.Contains(lt.AssignedToId)
            )
            .ToListAsync();

        // 3. Cargar cotizaciones del año de los asesores del supervisor
        var allQuotations = await _db
            .Quotations.Where(q =>
                q.CreatedAt >= startDate
                && q.CreatedAt < endDate
                && assignedAdvisorIds.Contains(q.AdvisorId)
            )
            .ToListAsync();

        // 4. Cargar reservaciones del año de los asesores del supervisor
        var allReservations = await _db
            .Reservations.Include(r => r.Quotation)
            .Where(r =>
                r.ReservationDate >= DateOnly.FromDateTime(startDate)
                && r.ReservationDate < DateOnly.FromDateTime(endDate)
                && r.Quotation != null
                && assignedAdvisorIds.Contains(r.Quotation!.AdvisorId)
            )
            .ToListAsync();

        // 5. Cargar solo los usuarios (asesores) asignados al supervisor
        var allUsers = await _db
            .Users.Where(u => u.IsActive && assignedAdvisorIds.Contains(u.Id))
            .ToListAsync();

        // --- CÁLCULOS EN MEMORIA (más eficiente) ---

        // KPIs de Leads
        var leadsKpi = new SupervisorLeadsKpiDto
        {
            TotalLeads = allLeads.Count,
            RegisteredLeads = allLeads.Count(l => l.Status == LeadStatus.Registered),
            AttendedLeads = allLeads.Count(l => l.Status == LeadStatus.Attended),
            InFollowUpLeads = allLeads.Count(l => l.Status == LeadStatus.InFollowUp),
            CompletedLeads = allLeads.Count(l => l.Status == LeadStatus.Completed),
            CanceledLeads = allLeads.Count(l => l.Status == LeadStatus.Canceled),
            ExpiredLeads = allLeads.Count(l => l.Status == LeadStatus.Expired),
            UnassignedLeads = allLeads.Count(l => !l.AssignedToId.HasValue),
        };

        // Métricas del equipo
        var completedLeads = allLeads.Count(l => l.Status == LeadStatus.Completed);
        var avgConversionRate =
            allLeads.Count > 0 ? Math.Round((completedLeads / (double)allLeads.Count) * 100, 1) : 0;

        var teamMetrics = new TeamMetricsDto
        {
            QuotationsGenerated = allQuotations.Count,
            ReservationsActive = allReservations.Count(r => r.Status == ReservationStatus.ISSUED),
            TasksToday = allTasks.Count(t => !t.IsCompleted && t.ScheduledDate.Date == now.Date),
            AvgConversionRate = avgConversionRate,
        };

        // Rendimiento de asesores
        var advisors = new List<AdvisorPerformanceDto>();
        foreach (var user in allUsers)
        {
            var userLeads = allLeads.Where(l => l.AssignedToId == user.Id).ToList();
            var userTasks = allTasks.Where(t => t.AssignedToId == user.Id).ToList();
            var userQuotations = allQuotations.Where(q => q.AdvisorId == user.Id).ToList();
            var userReservations = allReservations
                .Where(r => r.Quotation != null && r.Quotation.AdvisorId == user.Id)
                .ToList();

            var leadsAssigned = userLeads.Count;
            var leadsCompleted = userLeads.Count(l => l.Status == LeadStatus.Completed);
            var efficiency =
                leadsAssigned > 0
                    ? Math.Round((leadsCompleted / (double)leadsAssigned) * 100, 1)
                    : 0;

            // Solo incluir asesores con leads asignados
            if (leadsAssigned > 0)
            {
                advisors.Add(
                    new AdvisorPerformanceDto
                    {
                        Id = user.Id,
                        Name = user.Name,
                        LeadsAssigned = leadsAssigned,
                        LeadsInFollowUp = userLeads.Count(l => l.Status == LeadStatus.InFollowUp),
                        LeadsCompleted = leadsCompleted,
                        QuotationsIssued = userQuotations.Count,
                        ReservationsGenerated = userReservations.Count,
                        TasksCompleted = userTasks.Count(t => t.IsCompleted),
                        TasksPending = userTasks.Count(t => !t.IsCompleted),
                        AvgResponseTime = 2.5, // TODO: Calcular basado en timestamps
                        LastActivity = user.LastLogin,
                        Efficiency = efficiency,
                    }
                );
            }
        }

        // Ordenar por eficiencia descendente
        advisors = advisors.OrderByDescending(a => a.Efficiency).ToList();

        // --- TEAM DATA (similar al admin pero solo para asesores asignados) ---
        // Pre-cargar todos los datos necesarios en consultas optimizadas
        var userRolesDict = await (
            from ur in _db.UserRoles
            join r in _db.Roles on ur.RoleId equals r.Id
            where assignedAdvisorIds.Contains(ur.UserId)
            select new { UserId = ur.UserId, RoleName = r.Name }
        )
            .GroupBy(x => x.UserId)
            .ToDictionaryAsync(
                g => g.Key,
                g => g.First().RoleName // Toma el primer rol si hay múltiples
            );

        var quotationsByAdvisor = allQuotations
            .GroupBy(q => q.AdvisorId)
            .ToDictionary(g => g.Key, g => g.Count());

        var reservationsByAdvisor = allReservations
            .Where(r => r.Quotation != null)
            .GroupBy(r => r.Quotation!.AdvisorId)
            .ToDictionary(g => g.Key, g => g.Count());

        var teamData = new List<SupervisorTeamMemberDto>();
        foreach (var u in allUsers)
        {
            var role = userRolesDict.GetValueOrDefault(u.Id, "Sin rol");
            var quotations = quotationsByAdvisor.GetValueOrDefault(u.Id, 0);
            var reservations = reservationsByAdvisor.GetValueOrDefault(u.Id, 0);

            double efficiency =
                quotations > 0 ? Math.Round((reservations / (double)quotations) * 100, 1) : 0;

            teamData.Add(
                new SupervisorTeamMemberDto
                {
                    Name = u.Name,
                    Role = role,
                    Quotations = quotations,
                    Reservations = reservations,
                    Efficiency = efficiency,
                }
            );
        }

        // Selecciona solo el top 10 por eficiencia
        teamData = teamData
            .OrderByDescending(t => t.Efficiency)
            .ThenByDescending(t => t.Reservations)
            .Take(10)
            .ToList();

        // Leads recientes (últimos 10, priorizando sin asignar y próximos a expirar)
        var recentLeads = allLeads
            .OrderByDescending(l => !l.AssignedToId.HasValue) // Sin asignar primero
            .ThenBy(l => l.ExpirationDate) // Más próximos a expirar
            .Take(10)
            .Select(l => new RecentLeadDto
            {
                Id = l.Id,
                ClientName = l.Client?.Name ?? "Sin cliente",
                ClientPhone = l.Client?.PhoneNumber ?? "",
                CaptureSource = l.CaptureSource.ToString(),
                Status = l.Status.ToString(),
                DaysUntilExpiration = (int)(l.ExpirationDate - now).TotalDays,
                AssignedTo = l.AssignedTo?.Name,
                ProjectName = l.Project?.Name ?? "Sin proyecto",
                EntryDate = l.EntryDate,
                Priority = GetPriorityForLead(l, now),
            })
            .ToList();

        // Embudo de conversión
        var totalRegistered = allLeads.Count(l => l.Status != LeadStatus.Expired);
        var conversionFunnel = new List<ConversionFunnelDto>
        {
            new ConversionFunnelDto
            {
                Stage = "Registered",
                Count = leadsKpi.RegisteredLeads,
                Percentage = 100,
            },
            new ConversionFunnelDto
            {
                Stage = "Attended",
                Count = leadsKpi.AttendedLeads,
                Percentage =
                    totalRegistered > 0
                        ? Math.Round((leadsKpi.AttendedLeads / (double)totalRegistered) * 100, 1)
                        : 0,
            },
            new ConversionFunnelDto
            {
                Stage = "InFollowUp",
                Count = leadsKpi.InFollowUpLeads,
                Percentage =
                    totalRegistered > 0
                        ? Math.Round((leadsKpi.InFollowUpLeads / (double)totalRegistered) * 100, 1)
                        : 0,
            },
            new ConversionFunnelDto
            {
                Stage = "Completed",
                Count = leadsKpi.CompletedLeads,
                Percentage =
                    totalRegistered > 0
                        ? Math.Round((leadsKpi.CompletedLeads / (double)totalRegistered) * 100, 1)
                        : 0,
            },
        };

        // Distribución por fuente de captación
        var totalLeadsForSources = allLeads.Count;
        var leadSources = allLeads
            .GroupBy(l => l.CaptureSource)
            .Select(g => new LeadSourceDistributionDto
            {
                Source = g.Key.ToString(),
                Count = g.Count(),
                Percentage =
                    totalLeadsForSources > 0
                        ? Math.Round((g.Count() / (double)totalLeadsForSources) * 100, 1)
                        : 0,
                Color = GetColorForSource(g.Key),
            })
            .OrderByDescending(ls => ls.Count)
            .ToList();

        // Actividad semanal (últimos 7 días)
        var weeklyActivity = new List<WeeklyActivityDto>();
        var dayNames = new[] { "Dom", "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb" };

        for (int i = 6; i >= 0; i--)
        {
            var date = now.Date.AddDays(-i);
            var nextDate = date.AddDays(1);

            var dayLeads = allLeads.Where(l => l.EntryDate >= date && l.EntryDate < nextDate);

            weeklyActivity.Add(
                new WeeklyActivityDto
                {
                    Day = dayNames[(int)date.DayOfWeek],
                    NewLeads = dayLeads.Count(),
                    Assigned = dayLeads.Count(l => l.AssignedToId.HasValue),
                    Attended = dayLeads.Count(l => l.Status == LeadStatus.Attended),
                    Completed = dayLeads.Count(l => l.Status == LeadStatus.Completed),
                    Expired = dayLeads.Count(l => l.Status == LeadStatus.Expired),
                }
            );
        }

        // Análisis de tareas
        var taskAnalysis = allTasks
            .GroupBy(t => t.Type)
            .Select(g => new TaskAnalysisDto
            {
                Type = g.Key.ToString(),
                Scheduled = g.Count(),
                Completed = g.Count(t => t.IsCompleted),
                Pending = g.Count(t => !t.IsCompleted && t.ScheduledDate >= now),
                Overdue = g.Count(t => !t.IsCompleted && t.ScheduledDate < now),
            })
            .ToList();

        // Análisis por proyecto
        var projectLeads = allLeads
            .Where(l => l.ProjectId.HasValue)
            .GroupBy(l => l.ProjectId)
            .Select(g =>
            {
                var project = g.First().Project;
                var projectLeadsList = g.ToList();
                var completed = projectLeadsList.Count(l => l.Status == LeadStatus.Completed);
                var assigned = projectLeadsList.Count(l => l.AssignedToId.HasValue);

                // Calcular días promedio para completar
                var completedWithDates = projectLeadsList
                    .Where(l => l.Status == LeadStatus.Completed && l.ModifiedAt > l.EntryDate)
                    .ToList();

                var avgDays =
                    completedWithDates.Count > 0
                        ? Math.Round(
                            completedWithDates.Average(l => (l.ModifiedAt - l.EntryDate).TotalDays),
                            1
                        )
                        : 0;

                return new ProjectLeadsAnalysisDto
                {
                    Project = project?.Name ?? "Sin proyecto",
                    LeadsReceived = projectLeadsList.Count,
                    LeadsAssigned = assigned,
                    LeadsCompleted = completed,
                    ConversionRate =
                        projectLeadsList.Count > 0
                            ? Math.Round((completed / (double)projectLeadsList.Count) * 100, 1)
                            : 0,
                    AvgDaysToComplete = avgDays,
                };
            })
            .OrderByDescending(p => p.LeadsReceived)
            .ToList();

        return new SupervisorDashboardDto
        {
            LeadsKpi = leadsKpi,
            TeamMetrics = teamMetrics,
            Advisors = advisors,
            TeamData = teamData,
            RecentLeads = recentLeads,
            ConversionFunnel = conversionFunnel,
            LeadSources = leadSources,
            WeeklyActivity = weeklyActivity,
            TaskAnalysis = taskAnalysis,
            ProjectLeads = projectLeads,
        };
    }

    // Métodos auxiliares
    private string GetPriorityForLead(Lead lead, DateTime now)
    {
        var daysUntilExpiration = (lead.ExpirationDate - now).TotalDays;

        if (daysUntilExpiration <= 1 || !lead.AssignedToId.HasValue)
            return "high";
        if (daysUntilExpiration <= 3)
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
