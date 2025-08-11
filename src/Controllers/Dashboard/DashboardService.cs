using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

public class DashboardService
{
    private readonly DatabaseContext _db;

    public DashboardService(DatabaseContext db)
    {
        _db = db;
    }

    public async Task<DashboardAdminDto> GetDashboardAdminDataAsync(int? year = null)
    {
        var now = DateTime.UtcNow;
        var yearToUse = year ?? now.Year;

        var startDate = new DateTime(yearToUse, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(yearToUse + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        // Métricas principales
        var completedSalesCount = await _db.Reservations.CountAsync(r =>
            r.Status == ReservationStatus.CANCELED
            && r.ReservationDate >= DateOnly.FromDateTime(startDate)
            && r.ReservationDate < DateOnly.FromDateTime(endDate)
        );

        var leadsCount = await _db.Leads.CountAsync();

        var reservationsCount = await _db.Reservations.CountAsync(r =>
            r.ReservationDate >= DateOnly.FromDateTime(startDate)
            && r.ReservationDate < DateOnly.FromDateTime(endDate)
        );

        var monthlyRevenue =
            await _db
                .PaymentTransactions.Where(pt =>
                    pt.PaymentDate >= monthStart && pt.PaymentDate < monthEnd
                )
                .SumAsync(pt => (decimal?)pt.AmountPaid) ?? 0;

        var pendingPayments =
            await _db.Payments.Where(p => !p.Paid).SumAsync(p => (decimal?)p.AmountDue) ?? 0;

        var averageTicket =
            await _db
                .Quotations.Where(q => q.Status == QuotationStatus.ACCEPTED)
                .AverageAsync(q => (decimal?)q.FinalPrice) ?? 0;

        double conversionRate =
            leadsCount > 0 ? Math.Round((completedSalesCount / (double)leadsCount) * 100, 2) : 0;

        double operationalEfficiency =
            completedSalesCount > 0
                ? Math.Round((reservationsCount / (double)completedSalesCount) * 100, 2)
                : 0;

        // Lotes por estado (agrupamiento en memoria para evitar división por cero)
        var lotsGrouped = await _db
            .Lots.GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
            .ToListAsync();
        var totalLots = lotsGrouped.Sum(g => g.Count);
        var lotsByStatus = lotsGrouped
            .Select(g => new LotStatusDto
            {
                Status = g.Status,
                Count = g.Count,
                Percentage = totalLots > 0 ? Math.Round((g.Count / (double)totalLots) * 100, 1) : 0,
            })
            .ToList();

        // Leads por estado (sin porcentaje)
        var leadsByStatus = await _db
            .Leads.GroupBy(l => l.Status)
            .Select(g => new LeadStatusDto { Status = g.Key.ToString(), Count = g.Count() })
            .ToListAsync();

        // Fuentes de captación (agrupamiento en memoria para evitar división por cero)
        var sourcesGrouped = await _db
            .Leads.GroupBy(l => l.CaptureSource)
            .Select(g => new { Source = g.Key.ToString(), Count = g.Count() })
            .ToListAsync();
        var totalLeadsSources = sourcesGrouped.Sum(g => g.Count);
        var leadSources = sourcesGrouped
            .Select(g => new LeadSourceDto
            {
                Source = g.Source,
                Count = g.Count,
                Percentage =
                    totalLeadsSources > 0
                        ? Math.Round((g.Count / (double)totalLeadsSources) * 100, 1)
                        : 0,
            })
            .ToList();

        // Equipo de trabajo con rol principal (cálculo en memoria)
        var users = await _db.Users.ToListAsync();
        var teamData = new List<TeamMemberDto>();
        foreach (var u in users)
        {
            var role =
                (
                    from userRole in _db.UserRoles
                    join r in _db.Roles on userRole.RoleId equals r.Id
                    where userRole.UserId == u.Id
                    select r.Name
                ).FirstOrDefault() ?? "Sin rol";

            var quotations = await _db.Quotations.CountAsync(q => q.AdvisorId == u.Id);
            var reservations = await _db.Reservations.CountAsync(r =>
                r.Quotation.AdvisorId == u.Id
            );

            double efficiency =
                quotations > 0 ? Math.Round((reservations / (double)quotations) * 100, 1) : 0;

            teamData.Add(
                new TeamMemberDto
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

        // --- Análisis de clientes ---
        var clients = await _db.Clients.ToListAsync();
        var clientAnalysis = new ClientAnalysisDto
        {
            TotalClients = clients.Count,
            NaturalPersons = clients.Count(c => c.Type == ClientType.Natural),
            LegalEntities = clients.Count(c => c.Type == ClientType.Juridico),
            WithEmail = clients.Count(c => !string.IsNullOrWhiteSpace(c.Email)),
            WithCompleteData = clients.Count(c =>
                !string.IsNullOrWhiteSpace(c.Name)
                && !string.IsNullOrWhiteSpace(c.PhoneNumber)
                && !string.IsNullOrWhiteSpace(c.Email)
                && !string.IsNullOrWhiteSpace(c.Address)
            ),
            SeparateProperty = clients.Count(c => c.SeparateProperty),
            CoOwners = clients.Count(c => !string.IsNullOrWhiteSpace(c.CoOwners)),
        };

        // --- Métricas por proyecto ---
        var projects = await _db.Projects.Include(p => p.Blocks).ToListAsync();
        var blocks = await _db.Blocks.ToListAsync();
        var lots = await _db.Lots.ToListAsync();

        var projectMetrics = new List<ProjectMetricDto>();
        foreach (var project in projects)
        {
            var projectBlocks = blocks.Where(b => b.ProjectId == project.Id).ToList();
            var projectLots = lots.Where(l => projectBlocks.Any(b => b.Id == l.BlockId)).ToList();

            int available = projectLots.Count(l => l.Status == LotStatus.Available);
            int quoted = projectLots.Count(l => l.Status == LotStatus.Quoted);
            int reserved = projectLots.Count(l => l.Status == LotStatus.Reserved);
            int sold = projectLots.Count(l => l.Status == LotStatus.Sold);

            decimal revenue = projectLots.Where(l => l.Status == LotStatus.Sold).Sum(l => l.Price);
            decimal avgPrice = projectLots.Any()
                ? Math.Round(projectLots.Average(l => l.Price), 2)
                : 0;

            double efficiency =
                projectLots.Count > 0 ? Math.Round((sold / (double)projectLots.Count) * 100, 1) : 0;

            projectMetrics.Add(
                new ProjectMetricDto
                {
                    Name = project.Name,
                    Location = project.Location,
                    Blocks = projectBlocks.Count,
                    TotalLots = projectLots.Count,
                    Available = available,
                    Quoted = quoted,
                    Reserved = reserved,
                    Sold = sold,
                    Revenue = revenue,
                    AvgPrice = avgPrice,
                    Efficiency = efficiency,
                }
            );
        }

        // --- Métricas de pagos ---
        var totalScheduled = await _db.Payments.SumAsync(p => (decimal?)p.AmountDue) ?? 0;
        var totalPaid = await _db.PaymentTransactions.SumAsync(pt => (decimal?)pt.AmountPaid) ?? 0;
        var pending =
            await _db.Payments.Where(p => !p.Paid).SumAsync(p => (decimal?)p.AmountDue) ?? 0;
        var overdue =
            await _db
                .Payments.Where(p => !p.Paid && p.DueDate < DateTime.UtcNow)
                .SumAsync(p => (decimal?)p.AmountDue) ?? 0;

        var cashPayments =
            await _db
                .PaymentTransactions.Where(pt => pt.PaymentMethod == PaymentMethod.CASH)
                .SumAsync(pt => (decimal?)pt.AmountPaid) ?? 0;
        var bankTransfers =
            await _db
                .PaymentTransactions.Where(pt => pt.PaymentMethod == PaymentMethod.BANK_TRANSFER)
                .SumAsync(pt => (decimal?)pt.AmountPaid) ?? 0;
        var deposits =
            await _db
                .PaymentTransactions.Where(pt => pt.PaymentMethod == PaymentMethod.BANK_DEPOSIT)
                .SumAsync(pt => (decimal?)pt.AmountPaid) ?? 0;

        var paymentMetrics = new PaymentMetricsDto
        {
            TotalScheduled = totalScheduled,
            TotalPaid = totalPaid,
            Pending = pending,
            Overdue = overdue,
            CashPayments = cashPayments,
            BankTransfers = bankTransfers,
            Deposits = deposits,
        };

        // --- Rendimiento mensual ---
        var months = new[]
        {
            "Ene",
            "Feb",
            "Mar",
            "Abr",
            "May",
            "Jun",
            "Jul",
            "Ago",
            "Sep",
            "Oct",
            "Nov",
            "Dic",
        };
        var monthlyPerformance = new List<MonthlyPerformanceDto>();

        for (int m = 1; m <= 12; m++)
        {
            var monthStartDate = new DateTime(yearToUse, m, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEndDate = monthStartDate.AddMonths(1);

            // Leads por mes
            var leadsCount1 = await _db.Leads.CountAsync(l =>
                l.EntryDate >= monthStartDate && l.EntryDate < monthEndDate
            );

            // Cotizaciones por mes
            var quotationsInMonth = await _db
                .Quotations.Where(q => !string.IsNullOrEmpty(q.QuotationDate))
                .ToListAsync();

            var quotationsCount = quotationsInMonth.Count(q =>
            {
                DateTime date;
                if (DateTime.TryParse(q.QuotationDate, out date))
                {
                    return date >= monthStartDate && date < monthEndDate;
                }
                return false;
            });

            // Reservas por mes
            var reservationsCount1 = await _db.Reservations.CountAsync(r =>
                r.ReservationDate >= DateOnly.FromDateTime(monthStartDate)
                && r.ReservationDate < DateOnly.FromDateTime(monthEndDate)
            );

            // Ventas por mes (reservas con estado CANCELED)
            var salesCount = await _db.Reservations.CountAsync(r =>
                r.Status == ReservationStatus.CANCELED
                && r.ReservationDate >= DateOnly.FromDateTime(monthStartDate)
                && r.ReservationDate < DateOnly.FromDateTime(monthEndDate)
            );

            // Revenue por mes (pagos realizados en ese mes)
            var revenue =
                await _db
                    .PaymentTransactions.Where(pt =>
                        pt.PaymentDate >= monthStartDate && pt.PaymentDate < monthEndDate
                    )
                    .SumAsync(pt => (decimal?)pt.AmountPaid) ?? 0;

            monthlyPerformance.Add(
                new MonthlyPerformanceDto
                {
                    Month = months[m - 1],
                    Leads = leadsCount1,
                    Quotations = quotationsCount,
                    Reservations = reservationsCount1,
                    Sales = salesCount,
                    Revenue = revenue,
                }
            );
        }

        var dto = new DashboardAdminDto
        {
            TotalProjects = await _db.Projects.CountAsync(),
            TotalBlocks = await _db.Blocks.CountAsync(),
            TotalLots = totalLots,
            TotalClients = await _db.Clients.CountAsync(),
            ActiveLeads = await _db.Leads.CountAsync(l =>
                l.IsActive
                && l.Status != LeadStatus.Expired
                && l.Status != LeadStatus.Canceled
                && l.Status != LeadStatus.Completed
            ),
            ExpiredLeads = await _db.Leads.CountAsync(l => l.Status == LeadStatus.Expired),
            ActiveQuotations = await _db.Quotations.CountAsync(q =>
                q.Status == QuotationStatus.ISSUED || q.Status == QuotationStatus.ACCEPTED
            ),
            PendingReservations = await _db.Reservations.CountAsync(r =>
                r.Status == ReservationStatus.ISSUED
                && r.ReservationDate >= DateOnly.FromDateTime(startDate)
                && r.ReservationDate < DateOnly.FromDateTime(endDate)
            ),
            CompletedSales = completedSalesCount,
            MonthlyRevenue = monthlyRevenue,
            PendingPayments = pendingPayments,
            AverageTicket = averageTicket,
            ConversionRate = conversionRate,
            OperationalEfficiency = operationalEfficiency,
            LotsByStatus = lotsByStatus,
            LeadsByStatus = leadsByStatus,
            LeadSources = leadSources,
            TeamData = teamData,
            ClientAnalysis = clientAnalysis,
            ProjectMetrics = projectMetrics,
            PaymentMetrics = paymentMetrics,
            MonthlyPerformance = monthlyPerformance,
        };

        return dto;
    }
}
