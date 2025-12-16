using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Controllers;

public class GetManagerDashboardDataUseCase
{
    private readonly DatabaseContext _db;

    public GetManagerDashboardDataUseCase(DatabaseContext db)
    {
        _db = db;
    }

    public async Task<ManagerDashboardDto> ExecuteAsync(int? year = null)
    {
        var now = DateTime.UtcNow;
        var yearToUse = year ?? now.Year;

        var startDate = new DateTime(yearToUse, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(yearToUse + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // --- CONSULTAS OPTIMIZADAS: Una sola query por entidad ---

        // 1. Cargar todos los proyectos con sus bloques y lotes
        var allProjects = await _db
            .Projects.Include(p => p.Blocks)
            .ThenInclude(b => b.Lots)
            .Where(p => p.IsActive)
            .ToListAsync();

        // 2. Cargar todos los leads del año
        var allLeads = await _db
            .Leads.Include(l => l.Project)
            .Include(l => l.AssignedTo)
            .Where(l => l.EntryDate >= startDate && l.EntryDate < endDate)
            .ToListAsync();

        // 3. Cargar todas las cotizaciones del año
        var allQuotations = await _db
            .Quotations.Include(q => q.Advisor)
            .Include(q => q.Lead)
            .ThenInclude(l => l.Project)
            .Where(q => q.CreatedAt >= startDate && q.CreatedAt < endDate)
            .ToListAsync();

        // 4. Cargar todas las reservaciones del año
        var allReservations = await _db
            .Reservations.Include(r => r.Quotation)
            .ThenInclude(q => q.Advisor)
            .Include(r => r.Quotation)
            .ThenInclude(q => q.Lead)
            .ThenInclude(l => l.Project)
            .Where(r =>
                r.ReservationDate >= DateOnly.FromDateTime(startDate)
                && r.ReservationDate < DateOnly.FromDateTime(endDate)
            )
            .ToListAsync();

        // 5. Cargar todos los usuarios activos (asesores)
        var allAdvisors = await _db.Users.Where(u => u.IsActive).ToListAsync();

        // --- CÁLCULOS EN MEMORIA (más eficiente) ---

        // KPIs principales
        var totalLeads = allLeads.Count;
        var completedLeads = allLeads.Count(l => l.Status == LeadStatus.Completed);
        var totalReservationAmount = allReservations
            .Where(r => r.Status == ReservationStatus.ISSUED)
            .Sum(r => r.AmountPaid);

        var kpis = new ManagerKpisDto
        {
            TotalProjects = allProjects.Count(),
            ActiveProjects = allProjects.Count(p => p.IsActive),
            TotalLeads = totalLeads,
            TotalQuotations = allQuotations.Count,
            TotalReservations = allReservations.Count,
            TotalReservationAmount = totalReservationAmount,
            ConversionRate =
                totalLeads > 0 ? Math.Round((completedLeads / (decimal)totalLeads) * 100, 2) : 0,
            ActiveAdvisors = allAdvisors.Count,
        };

        // Rendimiento por proyecto
        var projectPerformance = allProjects
            .Select(project =>
            {
                var projectLeads = allLeads.Where(l => l.ProjectId == project.Id).ToList();
                var projectQuotations = allQuotations
                    .Where(q => q.Lead?.ProjectId == project.Id)
                    .ToList();
                var projectReservations = allReservations
                    .Where(r => r.Quotation?.Lead?.ProjectId == project.Id)
                    .ToList();

                var totalUnits = project.Blocks?.SelectMany(b => b.Lots).Count() ?? 0;
                var reservedUnits =
                    project
                        .Blocks?.SelectMany(b => b.Lots)
                        .Count(l => l.Status == LotStatus.Reserved) ?? 0;

                var projectCompletedLeads = projectLeads.Count(l =>
                    l.Status == LeadStatus.Completed
                );
                var projectConversionRate =
                    projectLeads.Count > 0
                        ? Math.Round((projectCompletedLeads / (decimal)projectLeads.Count) * 100, 2)
                        : 0;

                return new ProjectPerformanceDto
                {
                    ProjectId = project.Id,
                    ProjectName = project.Name,
                    TotalLeads = projectLeads.Count,
                    CompletedLeads = projectCompletedLeads,
                    Quotations = projectQuotations.Count,
                    Reservations = projectReservations.Count,
                    ReservationAmount = projectReservations
                        .Where(r => r.Status == ReservationStatus.ISSUED)
                        .Sum(r => r.AmountPaid),
                    ConversionRate = projectConversionRate,
                    AvailableUnits = totalUnits - reservedUnits,
                    ReservedUnits = reservedUnits,
                    OccupancyRate =
                        totalUnits > 0
                            ? Math.Round((reservedUnits / (decimal)totalUnits) * 100, 2)
                            : 0,
                };
            })
            .OrderByDescending(p => p.ReservationAmount)
            .ToList();

        // Rendimiento del equipo de ventas
        var salesTeamPerformance = allAdvisors
            .Select(advisor =>
            {
                var advisorLeads = allLeads.Where(l => l.AssignedToId == advisor.Id).ToList();
                var advisorQuotations = allQuotations
                    .Where(q => q.AdvisorId == advisor.Id)
                    .ToList();
                var advisorReservations = allReservations
                    .Where(r => r.Quotation?.AdvisorId == advisor.Id)
                    .ToList();

                var advisorCompletedLeads = advisorLeads.Count(l =>
                    l.Status == LeadStatus.Completed
                );
                var advisorConversionRate =
                    advisorLeads.Count > 0
                        ? Math.Round((advisorCompletedLeads / (decimal)advisorLeads.Count) * 100, 2)
                        : 0;

                return new SalesTeamPerformanceDto
                {
                    AdvisorId = advisor.Id,
                    AdvisorName = advisor.Name,
                    LeadsAssigned = advisorLeads.Count,
                    LeadsCompleted = advisorCompletedLeads,
                    QuotationsIssued = advisorQuotations.Count,
                    ReservationsGenerated = advisorReservations.Count,
                    ReservationAmount = advisorReservations
                        .Where(r => r.Status == ReservationStatus.ISSUED)
                        .Sum(r => r.AmountPaid),
                    ConversionRate = advisorConversionRate,
                    AvgResponseTime = 2.5m, // TODO: Calcular basado en timestamps reales
                };
            })
            .Where(a => a.LeadsAssigned > 0)
            .OrderByDescending(a => a.ReservationAmount)
            .ToList();

        // Análisis de fuentes de captación
        var leadSourceAnalysis = allLeads
            .GroupBy(l => l.CaptureSource)
            .Select(g =>
            {
                var sourceLeads = g.ToList();
                var convertedLeads = sourceLeads.Count(l => l.Status == LeadStatus.Completed);

                return new LeadSourceAnalysisDto
                {
                    Source = g.Key.ToString(),
                    TotalLeads = sourceLeads.Count,
                    ConvertedLeads = convertedLeads,
                    ConversionRate =
                        sourceLeads.Count > 0
                            ? Math.Round((convertedLeads / (decimal)sourceLeads.Count) * 100, 2)
                            : 0,
                    CostPerLead = 0, // TODO: Integrar con datos de marketing
                    ROI = 0, // TODO: Calcular basado en costos y revenue
                };
            })
            .OrderByDescending(s => s.ConversionRate)
            .ToList();

        // Análisis mensual de tendencias (últimos 6 meses)
        var monthlyTrends = new List<MonthlyTrendDto>();
        for (int i = 5; i >= 0; i--)
        {
            var monthStart = startDate.AddMonths(i);
            var monthEnd = monthStart.AddMonths(1);

            var monthLeads = allLeads.Where(l =>
                l.EntryDate >= monthStart && l.EntryDate < monthEnd
            );
            var monthQuotations = allQuotations.Where(q =>
                q.CreatedAt >= monthStart && q.CreatedAt < monthEnd
            );
            var monthReservations = allReservations.Where(r =>
                r.ReservationDate >= DateOnly.FromDateTime(monthStart)
                && r.ReservationDate < DateOnly.FromDateTime(monthEnd)
            );

            var monthLeadsCount = monthLeads.Count();
            var monthCompletedLeads = monthLeads.Count(l => l.Status == LeadStatus.Completed);

            monthlyTrends.Add(
                new MonthlyTrendDto
                {
                    Month = monthStart.ToString("MMM"),
                    Year = monthStart.Year,
                    LeadsReceived = monthLeadsCount,
                    QuotationsIssued = monthQuotations.Count(),
                    ReservationsMade = monthReservations.Count(),
                    ReservationAmount = monthReservations
                        .Where(r => r.Status == ReservationStatus.ISSUED)
                        .Sum(r => r.AmountPaid),
                    ConversionRate =
                        monthLeadsCount > 0
                            ? Math.Round((monthCompletedLeads / (decimal)monthLeadsCount) * 100, 2)
                            : 0,
                }
            );
        }

        // Pipeline de ventas
        var salesPipeline = new SalesPipelineDto
        {
            NewLeads = allLeads.Count(l => l.Status == LeadStatus.Registered),
            InContact = allLeads.Count(l => l.Status == LeadStatus.Attended),
            QuotationStage = allQuotations.Count(q => q.Status == QuotationStatus.ISSUED),
            NegotiationStage = allLeads.Count(l => l.Status == LeadStatus.InFollowUp),
            ReservationStage = allReservations.Count(r => r.Status == ReservationStatus.ISSUED),
            ClosedWon = allLeads.Count(l => l.Status == LeadStatus.Completed),
            ClosedLost = allLeads.Count(l =>
                l.Status == LeadStatus.Canceled || l.Status == LeadStatus.Expired
            ),
        };

        // Análisis de cotizaciones
        var quotationAnalysis = new QuotationAnalysisDto
        {
            TotalIssued = allQuotations.Count,
            Accepted = allQuotations.Count(q => q.Status == QuotationStatus.ACCEPTED),
            Pending = allQuotations.Count(q => q.Status == QuotationStatus.ISSUED),
            Rejected = allQuotations.Count(q => q.Status == QuotationStatus.CANCELED),
            AcceptanceRate =
                allQuotations.Count > 0
                    ? Math.Round(
                        (
                            allQuotations.Count(q => q.Status == QuotationStatus.ACCEPTED)
                            / (decimal)allQuotations.Count
                        ) * 100,
                        2
                    )
                    : 0,
            AvgQuotationAmount =
                allQuotations.Count > 0
                    ? Math.Round(allQuotations.Average(q => q.FinalPrice), 2)
                    : 0,
            TotalQuotationValue = allQuotations.Sum(q => q.FinalPrice),
        };

        // Análisis de reservaciones
        var reservationAnalysis = new ReservationAnalysisDto
        {
            TotalReservations = allReservations.Count,
            ActiveReservations = allReservations.Count(r => r.Status == ReservationStatus.ISSUED),
            CanceledReservations = allReservations.Count(r =>
                r.Status == ReservationStatus.CANCELED
            ),
            AnulatedReservations = allReservations.Count(r =>
                r.Status == ReservationStatus.ANULATED
            ),
            TotalReservationAmount = totalReservationAmount,
            AvgReservationAmount =
                allReservations.Count > 0
                    ? Math.Round(allReservations.Average(r => r.AmountPaid), 2)
                    : 0,
            CancellationRate =
                allReservations.Count > 0
                    ? Math.Round(
                        (
                            allReservations.Count(r => r.Status == ReservationStatus.CANCELED)
                            / (decimal)allReservations.Count
                        ) * 100,
                        2
                    )
                    : 0,
        };

        // Métricas de tiempo
        var timeMetrics = new TimeMetricsDto
        {
            AvgLeadToQuotation = 4.5m, // TODO: Calcular basado en timestamps reales
            AvgQuotationToReservation = 3.2m, // TODO: Calcular basado en timestamps reales
            AvgLeadToReservation = 7.7m, // TODO: Calcular basado en timestamps reales
            AvgResponseTime = 2.5m, // TODO: Calcular basado en timestamps reales
        };

        // Alertas para el manager
        var alerts = new List<ManagerAlertDto>();

        // Alerta: Leads sin asignar
        var unassignedLeads = allLeads.Count(l => !l.AssignedToId.HasValue);
        if (unassignedLeads > 0)
        {
            alerts.Add(
                new ManagerAlertDto
                {
                    Type = "warning",
                    Title = "Leads Sin Asignar",
                    Message = $"Hay {unassignedLeads} leads esperando asignación",
                    Count = unassignedLeads,
                    Priority = unassignedLeads > 10 ? "high" : "medium",
                }
            );
        }

        // Alerta: Leads próximos a expirar
        var expiringLeads = allLeads.Count(l =>
            l.ExpirationDate <= now.AddDays(2) && l.Status != LeadStatus.Completed
        );
        if (expiringLeads > 0)
        {
            alerts.Add(
                new ManagerAlertDto
                {
                    Type = "danger",
                    Title = "Leads Próximos a Expirar",
                    Message = $"{expiringLeads} leads expiran en menos de 2 días",
                    Count = expiringLeads,
                    Priority = "high",
                }
            );
        }

        // Alerta: Cotizaciones pendientes
        var pendingQuotations = allQuotations.Count(q => q.Status == QuotationStatus.ISSUED);
        if (pendingQuotations > 20)
        {
            alerts.Add(
                new ManagerAlertDto
                {
                    Type = "info",
                    Title = "Cotizaciones Pendientes",
                    Message = $"{pendingQuotations} cotizaciones esperando respuesta",
                    Count = pendingQuotations,
                    Priority = "medium",
                }
            );
        }

        // Top performers
        var topPerformers = new List<TopPerformerDto>();

        // Top advisor por conversiones
        var topAdvisorByConversions = salesTeamPerformance
            .OrderByDescending(a => a.LeadsCompleted)
            .FirstOrDefault();
        if (topAdvisorByConversions != null)
        {
            topPerformers.Add(
                new TopPerformerDto
                {
                    Name = topAdvisorByConversions.AdvisorName,
                    Category = "advisor",
                    Value = topAdvisorByConversions.LeadsCompleted,
                    Metric = "conversions",
                }
            );
        }

        // Top proyecto por revenue
        var topProjectByRevenue = projectPerformance
            .OrderByDescending(p => p.ReservationAmount)
            .FirstOrDefault();
        if (topProjectByRevenue != null)
        {
            topPerformers.Add(
                new TopPerformerDto
                {
                    Name = topProjectByRevenue.ProjectName,
                    Category = "project",
                    Value = topProjectByRevenue.ReservationAmount,
                    Metric = "revenue",
                }
            );
        }

        // Top fuente por conversión
        var topSourceByConversion = leadSourceAnalysis
            .OrderByDescending(s => s.ConversionRate)
            .FirstOrDefault();
        if (topSourceByConversion != null)
        {
            topPerformers.Add(
                new TopPerformerDto
                {
                    Name = topSourceByConversion.Source,
                    Category = "source",
                    Value = topSourceByConversion.ConversionRate,
                    Metric = "conversion_rate",
                }
            );
        }

        return new ManagerDashboardDto
        {
            Kpis = kpis,
            ProjectPerformance = projectPerformance,
            SalesTeamPerformance = salesTeamPerformance,
            LeadSourceAnalysis = leadSourceAnalysis,
            MonthlyTrends = monthlyTrends,
            SalesPipeline = salesPipeline,
            QuotationAnalysis = quotationAnalysis,
            ReservationAnalysis = reservationAnalysis,
            TimeMetrics = timeMetrics,
            Alerts = alerts,
            TopPerformers = topPerformers,
        };
    }
}
