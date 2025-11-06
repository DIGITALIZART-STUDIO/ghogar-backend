using System.Text.Json;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

public class GetDashboardAdminDataUseCase
{
    private readonly DatabaseContext _db;

    public GetDashboardAdminDataUseCase(DatabaseContext db)
    {
        _db = db;
    }

    public async Task<DashboardAdminDto> ExecuteAsync(int? year = null)
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

        // Revenue anual = PaymentTransactions + Reservaciones CANCELED (AmountPaid de reservas)
        // Debug: verificar datos de PaymentTransactions
        var paymentTransactionsDebug = await _db
            .PaymentTransactions.Where(pt => pt.PaymentDate.Year == yearToUse)
            .Select(pt => new
            {
                Id = pt.Id,
                PaymentDate = pt.PaymentDate,
                AmountPaid = pt.AmountPaid,
                Year = pt.PaymentDate.Year,
            })
            .ToListAsync();

        // Debug: verificar datos de Reservations
        var reservationsDebug = await _db
            .Reservations.Where(r =>
                r.Status == ReservationStatus.CANCELED
                && r.ReservationDate >= DateOnly.FromDateTime(startDate)
                && r.ReservationDate < DateOnly.FromDateTime(endDate)
            )
            .Select(r => new
            {
                Id = r.Id,
                ReservationDate = r.ReservationDate,
                AmountPaid = r.AmountPaid,
                Status = r.Status,
                StartDate = DateOnly.FromDateTime(startDate),
                EndDate = DateOnly.FromDateTime(endDate),
            })
            .ToListAsync();

        // Cálculo directo usando los datos ya consultados
        var paymentTransactionsSum = paymentTransactionsDebug.Sum(pt => pt.AmountPaid);
        var reservationsSum = reservationsDebug.Sum(r => r.AmountPaid);
        var annualRevenue = paymentTransactionsSum + reservationsSum;

        // Debug: verificar los valores calculados
        var transactionCount = paymentTransactionsDebug.Count;
        var reservationCount = reservationsDebug.Count;
        var transactionSum = paymentTransactionsSum;
        var reservationSum = reservationsSum;

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

        // Equipo de trabajo con rol principal (optimizado para evitar N+1)
        var users = await _db.Users.ToListAsync();

        // Pre-cargar todos los datos necesarios en consultas optimizadas
        var userRolesDict = await (
            from ur in _db.UserRoles
            join r in _db.Roles on ur.RoleId equals r.Id
            select new { UserId = ur.UserId, RoleName = r.Name }
        )
            .GroupBy(x => x.UserId)
            .ToDictionaryAsync(
                g => g.Key,
                g => g.First().RoleName // Toma el primer rol si hay múltiples
            );

        var quotationsByAdvisor = await _db
            .Quotations.GroupBy(q => q.AdvisorId)
            .Select(g => new { AdvisorId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.AdvisorId, x => x.Count);

        var reservationsByAdvisor = await _db
            .Reservations.Include(r => r.Quotation)
            .GroupBy(r => r.Quotation.AdvisorId)
            .Select(g => new { AdvisorId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.AdvisorId, x => x.Count);

        var teamData = new List<TeamMemberDto>();
        foreach (var u in users)
        {
            var role = userRolesDict.GetValueOrDefault(u.Id, "Sin rol");
            var quotations = quotationsByAdvisor.GetValueOrDefault(u.Id, 0);
            var reservations = reservationsByAdvisor.GetValueOrDefault(u.Id, 0);

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

        // --- Datos de clientes: registros mensuales, geografía y recientes ---
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

        // --- Datos de clientes: registros mensuales (optimizado) ---
        // Pre-cargar todos los clientes del año una sola vez
        var yearClients = await _db
            .Clients.Where(c => c.CreatedAt >= startDate && c.CreatedAt < endDate)
            .Select(c => new { c.CreatedAt, c.Type })
            .ToListAsync();

        var clientRegistrations = new List<ClientRegistrationDto>();
        for (int m = 1; m <= 12; m++)
        {
            var monthStartDate = new DateTime(yearToUse, m, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEndDate = monthStartDate.AddMonths(1);

            // Filtrar en memoria (mucho más rápido)
            var monthClients = yearClients.Where(c =>
                c.CreatedAt >= monthStartDate && c.CreatedAt < monthEndDate
            );

            var naturalCount = monthClients.Count(c => c.Type == ClientType.Natural);
            var juridicoCount = monthClients.Count(c => c.Type == ClientType.Juridico);

            clientRegistrations.Add(
                new ClientRegistrationDto
                {
                    Month = months[m - 1],
                    Natural = naturalCount,
                    Juridico = juridicoCount,
                }
            );
        }

        // Agrupación geográfica por país
        var countryGroups = await _db
            .Clients.GroupBy(c => string.IsNullOrEmpty(c.Country) ? "Desconocido" : c.Country)
            .Select(g => new { Country = g.Key, Count = g.Count() })
            .ToListAsync();

        var totalClientsForGeo = countryGroups.Sum(g => g.Count);
        var geographicData = countryGroups
            .Select(g => new GeographicClientDto
            {
                Country = g.Country,
                Count = g.Count,
                Percentage =
                    totalClientsForGeo > 0
                        ? Math.Round((g.Count / (double)totalClientsForGeo) * 100, 1)
                        : 0,
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        // Clientes recientes (top 4) y cálculo de completitud y días
        var recentClientsEntities = await _db
            .Clients.OrderByDescending(c => c.CreatedAt)
            .Take(4)
            .ToListAsync();

        var recentClients = recentClientsEntities
            .Select(c =>
            {
                // Calcular "completitud" simple: Name, PhoneNumber, Email, Address, Country (5 campos)
                int totalFields = 5;
                int filled = 0;
                if (!string.IsNullOrWhiteSpace(c.Name))
                    filled++;
                if (!string.IsNullOrWhiteSpace(c.PhoneNumber))
                    filled++;
                if (!string.IsNullOrWhiteSpace(c.Email))
                    filled++;
                if (!string.IsNullOrWhiteSpace(c.Address))
                    filled++;
                if (!string.IsNullOrWhiteSpace(c.Country))
                    filled++;

                int completeness = (int)Math.Round((filled / (double)totalFields) * 100);

                return new RecentClientDto
                {
                    Name = c.Name ?? "",
                    Type = c.Type == ClientType.Natural ? "Natural" : "Juridico",
                    Phone = c.PhoneNumber ?? "",
                    Email = string.IsNullOrWhiteSpace(c.Email) ? null : c.Email,
                    Country = c.Country ?? "Desconocido",
                    Completeness = completeness,
                    DaysAgo = (int)(DateTime.UtcNow.Date - c.CreatedAt.Date).TotalDays,
                    HasCoOwners = !string.IsNullOrWhiteSpace(c.CoOwners),
                    SeparateProperty = c.SeparateProperty,
                };
            })
            .ToList();

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

        // --- Rendimiento mensual (optimizado sin concurrencia) ---
        // Realizar consultas secuencialmente para evitar problemas de concurrencia en DbContext
        var yearLeads = await _db
            .Leads.Where(l => l.EntryDate >= startDate && l.EntryDate < endDate)
            .ToListAsync();

        var yearQuotations = await _db
            .Quotations.Where(q => !string.IsNullOrEmpty(q.QuotationDate))
            .ToListAsync();

        var yearReservations = await _db
            .Reservations.Where(r =>
                r.ReservationDate >= DateOnly.FromDateTime(startDate)
                && r.ReservationDate < DateOnly.FromDateTime(endDate)
            )
            .ToListAsync();

        var yearPayments = await _db
            .PaymentTransactions.Where(pt => pt.PaymentDate.Year == yearToUse)
            .ToListAsync();

        var monthlyPerformance = new List<MonthlyPerformanceDto>();

        for (int m = 1; m <= 12; m++)
        {
            var monthStartDate = new DateTime(yearToUse, m, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEndDate = monthStartDate.AddMonths(1);

            // Leads por mes (filtrado en memoria)
            var monthLeadsCount = yearLeads.Count(l =>
                l.EntryDate >= monthStartDate && l.EntryDate < monthEndDate
            );

            // Cotizaciones por mes (filtrado en memoria)
            var monthQuotationsCount = yearQuotations.Count(q =>
            {
                if (DateTime.TryParse(q.QuotationDate, out DateTime date))
                {
                    return date >= monthStartDate && date < monthEndDate;
                }
                return false;
            });

            // Reservas por mes (filtrado en memoria)
            var monthReservationsCount = yearReservations.Count(r =>
                r.ReservationDate >= DateOnly.FromDateTime(monthStartDate)
                && r.ReservationDate < DateOnly.FromDateTime(monthEndDate)
            );

            // Ventas por mes (filtrado en memoria - reservas completadas)
            var monthSalesCount = yearReservations.Count(r =>
                r.Status == ReservationStatus.CANCELED
                && r.ReservationDate >= DateOnly.FromDateTime(monthStartDate)
                && r.ReservationDate < DateOnly.FromDateTime(monthEndDate)
            );

            // Revenue por mes (filtrado en memoria)
            var monthRevenue = yearPayments
                .Where(pt => pt.PaymentDate >= monthStartDate && pt.PaymentDate < monthEndDate)
                .Sum(pt => pt.AmountPaid);

            monthlyPerformance.Add(
                new MonthlyPerformanceDto
                {
                    Month = months[m - 1],
                    Leads = monthLeadsCount,
                    Quotations = monthQuotationsCount,
                    Reservations = monthReservationsCount,
                    Sales = monthSalesCount,
                    Revenue = monthRevenue,
                }
            );
        }

        // --- Nuevas métricas financieras optimizadas ---

        // Pipeline de pagos - Una sola consulta por cada entidad (filtro en memoria por año)
        var allQuotations = await _db.Quotations.ToListAsync();
        var allReservations = await _db
            .Reservations.Where(r =>
                r.ReservationDate >= DateOnly.FromDateTime(startDate)
                && r.ReservationDate < DateOnly.FromDateTime(endDate)
            )
            .ToListAsync();
        var allPayments = await _db
            .Payments.Where(p => p.DueDate >= startDate && p.DueDate < endDate)
            .ToListAsync();
        var allPaymentTransactions = await _db
            .PaymentTransactions.Where(pt => pt.PaymentDate.Year == yearToUse)
            .ToListAsync();

        // Pipeline de pagos calculado en memoria
        var paymentPipeline = new List<PaymentPipelineStageDto>
        {
            new PaymentPipelineStageDto
            {
                Stage = "Cotizaciones",
                Count = allQuotations.Count(q =>
                    q.Status == QuotationStatus.ISSUED || q.Status == QuotationStatus.ACCEPTED
                ),
                Amount = allQuotations
                    .Where(q => q.Status == QuotationStatus.ACCEPTED)
                    .Sum(q => q.FinalPrice),
            },
            new PaymentPipelineStageDto
            {
                Stage = "Separaciones",
                Count = allReservations.Count(),
                Amount = allReservations.Sum(r => r.AmountPaid), // Solo separaciones iniciales
            },
            new PaymentPipelineStageDto
            {
                Stage = "Cronogramas",
                Count = allReservations.Count(r => !string.IsNullOrEmpty(r.Schedule)),
                Amount = allPayments.Sum(p => p.AmountDue), // Monto total programado
            },
            new PaymentPipelineStageDto
            {
                Stage = "Pagos Realizados",
                Count = allPaymentTransactions.Count(),
                Amount = allPaymentTransactions.Sum(pt => pt.AmountPaid), // Ingresos reales
            },
        };

        // Estado de reservaciones calculado en memoria
        var totalReservations = allReservations.Count;
        var reservationStatusData = allReservations
            .GroupBy(r => r.Status)
            .Select(g => new DashboardReservationStatusDto
            {
                Status = g.Key.ToString(),
                Count = g.Count(),
                Amount = g.Sum(r => r.AmountPaid), // Monto de separaciones por estado
                Percentage =
                    totalReservations > 0
                        ? Math.Round((g.Count() / (double)totalReservations) * 100, 1)
                        : 0,
            })
            .ToList();

        // Métodos de pago calculados en memoria
        var totalTransactions = allPaymentTransactions.Count;
        var totalTransactionAmount = allPaymentTransactions.Sum(pt => pt.AmountPaid);
        var paymentMethodsData = allPaymentTransactions
            .GroupBy(pt => pt.PaymentMethod)
            .Select(g => new PaymentMethodDto
            {
                Method = g.Key.ToString(),
                Count = g.Count(),
                Amount = g.Sum(pt => pt.AmountPaid),
                Percentage =
                    totalTransactions > 0
                        ? Math.Round((g.Count() / (double)totalTransactions) * 100, 1)
                        : 0,
            })
            .OrderByDescending(pm => pm.Amount)
            .ToList();

        // Próximos pagos - obtener solo los próximos 30 días con relaciones cargadas
        var next30Days = DateTime.UtcNow.AddDays(30);
        var upcomingPaymentsQuery = await _db
            .Payments.Include(p => p.Reservation)
            .ThenInclude(r => r.Client)
            .Where(p => !p.Paid && p.DueDate <= next30Days && p.DueDate >= DateTime.UtcNow)
            .OrderBy(p => p.DueDate)
            .Take(10)
            .ToListAsync();

        var upcomingPayments = upcomingPaymentsQuery
            .Select(p => new UpcomingPaymentDto
            {
                ReservationId = p.ReservationId,
                ClientName = p.Reservation?.Client?.Name ?? "Cliente no encontrado",
                AmountDue = p.AmountDue,
                DueDate = p.DueDate,
                DaysLeft = (int)(p.DueDate.Date - DateTime.UtcNow.Date).TotalDays,
                Status =
                    (int)(p.DueDate.Date - DateTime.UtcNow.Date).TotalDays <= 3 ? "urgent"
                    : (int)(p.DueDate.Date - DateTime.UtcNow.Date).TotalDays <= 7 ? "warning"
                    : "normal",
            })
            .ToList();

        // Flujo de caja mensual - calculado en memoria usando datos ya cargados
        var cashFlowData = new List<CashFlowDto>();

        for (int m = 1; m <= 12; m++)
        {
            var monthStartDate = new DateTime(yearToUse, m, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEndDate = monthStartDate.AddMonths(1);

            // Pagos programados para este mes (lo que deberían pagar)
            var scheduledMonth = allPayments
                .Where(p => p.DueDate >= monthStartDate && p.DueDate < monthEndDate)
                .Sum(p => p.AmountDue);

            // Pagos realizados en este mes (ingresos reales de PaymentTransactions)
            var realizedMonth = allPaymentTransactions
                .Where(pt => pt.PaymentDate >= monthStartDate && pt.PaymentDate < monthEndDate)
                .Sum(pt => pt.AmountPaid);

            // Separaciones realizadas en este mes (pagos iniciales de Reservations)
            var separationsMonth = allReservations
                .Where(r =>
                    r.ReservationDate >= DateOnly.FromDateTime(monthStartDate)
                    && r.ReservationDate < DateOnly.FromDateTime(monthEndDate)
                )
                .Sum(r => r.AmountPaid);

            cashFlowData.Add(
                new CashFlowDto
                {
                    Month = months[m - 1],
                    Programado = scheduledMonth,
                    Realizado = realizedMonth, // Solo PaymentTransactions
                    Separaciones = separationsMonth, // Solo Reservation.AmountPaid
                }
            );
        }

        // --- Construcción final del DTO ---
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
            AnnualRevenue = annualRevenue,
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
            ClientRegistrations = clientRegistrations,
            GeographicData = geographicData,
            RecentClients = recentClients,
            // Nuevas propiedades financieras
            PaymentPipeline = paymentPipeline,
            ReservationStatusData = reservationStatusData,
            PaymentMethodsData = paymentMethodsData,
            UpcomingPayments = upcomingPayments,
            CashFlowData = cashFlowData,
        };

        return dto;
    }
}
