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
        };

        return dto;
    }
}
