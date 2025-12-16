using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

public class GetFinanceManagerDashboardDataUseCase
{
    private readonly DatabaseContext _db;

    public GetFinanceManagerDashboardDataUseCase(DatabaseContext db)
    {
        _db = db;
    }

    public async Task<FinanceManagerDashboardDto> ExecuteAsync(
        int? year = null,
        Guid? projectId = null
    )
    {
        var now = DateTime.UtcNow;
        var yearToUse = year ?? now.Year;

        var startDate = new DateTime(yearToUse, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(yearToUse + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // --- CONSULTAS OPTIMIZADAS: SOLO DATOS NECESARIOS ---

        // 1. Obtener solo los datos necesarios de cotizaciones aceptadas
        var acceptedQuotationsQuery = _db
            .Quotations.Where(q => q.Status == QuotationStatus.ACCEPTED)
            .Select(q => new
            {
                q.Id,
                q.FinalPrice,
                ProjectId = q.Lot.Block.ProjectId,
                ProjectName = q.Lot.Block.Project.Name,
            });

        if (projectId.HasValue)
        {
            acceptedQuotationsQuery = acceptedQuotationsQuery.Where(q =>
                q.ProjectId == projectId.Value
            );
        }

        var acceptedQuotations = await acceptedQuotationsQuery.ToListAsync();

        // 2. Obtener solo datos necesarios de reservaciones
        var reservationsQuery = _db
            .Reservations.Where(r =>
                r.ReservationDate >= DateOnly.FromDateTime(startDate)
                && r.ReservationDate < DateOnly.FromDateTime(endDate)
            )
            .Select(r => new
            {
                r.Id,
                r.AmountPaid,
                r.Status,
                r.ReservationDate,
                ProjectId = r.Quotation.Lot.Block.ProjectId,
                ProjectName = r.Quotation.Lot.Block.Project.Name,
                ClientName = r.Client.Name,
            });

        if (projectId.HasValue)
        {
            reservationsQuery = reservationsQuery.Where(r => r.ProjectId == projectId.Value);
        }

        var reservations = await reservationsQuery.ToListAsync();

        // 3. Obtener solo datos necesarios de pagos programados
        var paymentsQuery = _db.Payments.Select(p => new
        {
            p.Id,
            p.AmountDue,
            p.DueDate,
            p.Paid,
            p.ReservationId,
            ProjectId = p.Reservation.Quotation.Lot.Block.ProjectId,
            ProjectName = p.Reservation.Quotation.Lot.Block.Project.Name,
            ClientName = p.Reservation.Client.Name,
            LotNumber = p.Reservation.Quotation.Lot.LotNumber,
        });

        if (projectId.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(p => p.ProjectId == projectId.Value);
        }

        var payments = await paymentsQuery.ToListAsync();

        // 4. Obtener solo datos necesarios de transacciones de pago
        var paymentTransactionsQuery = _db
            .PaymentTransactions.Where(pt => pt.PaymentDate.Year == yearToUse)
            .Select(pt => new
            {
                pt.Id,
                pt.AmountPaid,
                pt.PaymentDate,
                ProjectId = pt.Reservation != null
                    ? pt.Reservation.Quotation.Lot.Block.ProjectId
                    : (Guid?)null,
                ProjectName = pt.Reservation != null
                    ? pt.Reservation.Quotation.Lot.Block.Project.Name
                    : null,
            });

        if (projectId.HasValue)
        {
            paymentTransactionsQuery = paymentTransactionsQuery.Where(pt =>
                pt.ProjectId == projectId.Value
            );
        }

        var paymentTransactions = await paymentTransactionsQuery.ToListAsync();

        // --- CÁLCULOS FINANCIEROS OPTIMIZADOS ---

        // Resumen financiero
        var totalInvoiced = acceptedQuotations.Sum(q => (decimal)q.FinalPrice);

        var totalCollected =
            paymentTransactions.Sum(pt => (decimal)pt.AmountPaid)
            + reservations
                .Where(r => r.Status == ReservationStatus.CANCELED)
                .Sum(r => (decimal)r.AmountPaid);

        var pendingCollection = payments.Where(p => !p.Paid).Sum(p => (decimal)p.AmountDue);

        var overdue = payments
            .Where(p => !p.Paid && (DateTime)p.DueDate < now)
            .Sum(p => (decimal)p.AmountDue);

        var grossMargin =
            totalInvoiced > 0
                ? Math.Round(
                    (double)((totalInvoiced - (totalInvoiced * 0.315m)) / totalInvoiced) * 100,
                    1
                )
                : 0;

        var portfolioTurnover =
            totalInvoiced > 0 ? Math.Round((double)(totalCollected / totalInvoiced) * 100, 1) : 0;

        // Calcular proyección mensual
        var nextMonth = now.AddMonths(1);
        var nextMonthStart = new DateTime(nextMonth.Year, nextMonth.Month, 1);
        var nextMonthEnd = nextMonthStart.AddMonths(1);
        var monthlyProjection = payments
            .Where(p =>
                !p.Paid
                && (DateTime)p.DueDate >= nextMonthStart
                && (DateTime)p.DueDate < nextMonthEnd
            )
            .Sum(p => (decimal)p.AmountDue);

        var financialSummary = new FinancialSummaryDto
        {
            TotalInvoiced = totalInvoiced,
            TotalCollected = totalCollected,
            PendingCollection = pendingCollection,
            Overdue = overdue,
            CurrentLiquidity = totalCollected,
            MonthlyProjection = monthlyProjection,
            GrossMargin = grossMargin,
            PortfolioTurnover = portfolioTurnover,
        };

        // Cuentas por cobrar por proyecto (optimizado)
        var accountsReceivable = CalculateAccountsReceivableByProjectOptimized(
            acceptedQuotations,
            reservations,
            payments,
            paymentTransactions,
            projectId
        );

        // Ingresos mensuales (últimos 6 meses) - optimizado
        var monthlyIncome = CalculateMonthlyIncomeOptimized(
            paymentTransactions,
            reservations,
            yearToUse,
            projectId
        );

        // Cronograma de pagos próximos (próximos 30 días) - optimizado
        var paymentSchedule = CalculatePaymentScheduleOptimized(payments, now);

        // Análisis de morosidad - optimizado
        var delinquencyAnalysis = CalculateDelinquencyAnalysisOptimized(payments, now);

        // Proyección de ingresos (próximos 3 meses) - optimizado
        var incomeProjection = CalculateIncomeProjectionOptimized(payments, now);

        // Indicadores KPI - optimizado
        var portfolioDays = CalculatePortfolioDaysOptimized(payments, now);

        var monthlyGrowth = CalculateMonthlyGrowthOptimized(monthlyIncome);

        var kpiIndicators = new KpiIndicatorsDto
        {
            PortfolioDays = portfolioDays,
            LiquidityIndex =
                totalCollected > 0
                    ? Math.Round((double)(totalCollected / Math.Max(overdue, 1)), 1)
                    : 0,
            AssetTurnover =
                totalInvoiced > 0 ? Math.Round((double)(totalCollected / totalInvoiced), 1) : 0,
            OperatingMargin = grossMargin,
            MonthlyGrowth = monthlyGrowth,
            CollectionEfficiency =
                totalInvoiced > 0
                    ? Math.Round((double)(totalCollected / totalInvoiced) * 100, 1)
                    : 0,
        };

        var result = new FinanceManagerDashboardDto
        {
            FinancialSummary = financialSummary,
            AccountsReceivable = accountsReceivable,
            MonthlyIncome = monthlyIncome,
            PaymentSchedule = paymentSchedule,
            DelinquencyAnalysis = delinquencyAnalysis,
            IncomeProjection = incomeProjection,
            KpiIndicators = kpiIndicators,
        };
        return result;
    }

    // --- MÉTODOS OPTIMIZADOS ---

    private List<AccountsReceivableDto> CalculateAccountsReceivableByProjectOptimized(
        IEnumerable<dynamic> acceptedQuotations,
        IEnumerable<dynamic> reservations,
        IEnumerable<dynamic> payments,
        IEnumerable<dynamic> paymentTransactions,
        Guid? projectId
    )
    {
        // Agrupar por proyecto usando los datos ya cargados
        var projectGroups = acceptedQuotations
            .GroupBy(q => new { q.ProjectId, q.ProjectName })
            .ToList();

        if (projectId.HasValue)
        {
            projectGroups = projectGroups.Where(g => g.Key.ProjectId == projectId.Value).ToList();
        }

        var result = new List<AccountsReceivableDto>();

        foreach (var group in projectGroups)
        {
            var projectIdValue = group.Key.ProjectId;
            var projectName = group.Key.ProjectName;

            var projectQuotations = acceptedQuotations.Where(q => q.ProjectId == projectIdValue);
            var projectReservations = reservations.Where(r => r.ProjectId == projectIdValue);
            var projectPayments = payments.Where(p => p.ProjectId == projectIdValue);
            var projectTransactions = paymentTransactions.Where(pt =>
                pt.ProjectId == projectIdValue
            );

            var invoiced = projectQuotations.Sum(q => (decimal)q.FinalPrice);

            var collected =
                projectTransactions.Sum(pt => (decimal)pt.AmountPaid)
                + projectReservations
                    .Where(r => r.Status == ReservationStatus.CANCELED)
                    .Sum(r => (decimal)r.AmountPaid);

            var pending = projectPayments.Where(p => !p.Paid).Sum(p => (decimal)p.AmountDue);

            var overdue = projectPayments
                .Where(p => !p.Paid && (DateTime)p.DueDate < DateTime.UtcNow)
                .Sum(p => (decimal)p.AmountDue);

            var nextPayment = projectPayments
                .Where(p => !p.Paid && (DateTime)p.DueDate >= DateTime.UtcNow)
                .OrderBy(p => (DateTime)p.DueDate)
                .FirstOrDefault();

            result.Add(
                new AccountsReceivableDto
                {
                    Project = projectName,
                    Invoiced = invoiced,
                    Collected = collected,
                    Pending = pending,
                    Overdue = overdue,
                    NextDue = nextPayment?.AmountDue ?? 0,
                    NextPaymentDate =
                        nextPayment != null
                            ? ((DateTime)nextPayment.DueDate).ToString("yyyy-MM-dd")
                            : "",
                }
            );
        }

        return result.OrderByDescending(a => a.Invoiced).ToList();
    }

    private List<MonthlyIncomeDto> CalculateMonthlyIncomeOptimized(
        IEnumerable<dynamic> paymentTransactions,
        IEnumerable<dynamic> reservations,
        int year,
        Guid? projectId
    )
    {
        var result = new List<MonthlyIncomeDto>();
        decimal accumulated = 0;

        // Obtener todos los meses únicos que tienen datos
        var transactionMonths = paymentTransactions
            .Select(pt => ((DateTimeOffset)pt.PaymentDate).DateTime.Month)
            .Distinct();

        var reservationMonths = reservations
            .Where(r => r.Status == ReservationStatus.CANCELED)
            .Select(r => ((DateOnly)r.ReservationDate).Month)
            .Distinct();

        var allMonths = transactionMonths.Concat(reservationMonths).Distinct().OrderBy(m => m);

        // Si no hay datos, mostrar los últimos 6 meses del año
        if (!allMonths.Any())
        {
            allMonths = Enumerable.Range(7, 6).OrderBy(m => m); // Jul-Dec
        }

        var monthNames = new[]
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

        foreach (var month in allMonths)
        {
            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var monthTransactions = paymentTransactions.Where(pt =>
                ((DateTimeOffset)pt.PaymentDate).DateTime >= monthStart
                && ((DateTimeOffset)pt.PaymentDate).DateTime < monthEnd
            );

            var monthReservations = reservations.Where(r =>
                r.Status == ReservationStatus.CANCELED
                && (DateOnly)r.ReservationDate >= DateOnly.FromDateTime(monthStart)
                && (DateOnly)r.ReservationDate < DateOnly.FromDateTime(monthEnd)
            );

            if (projectId.HasValue)
            {
                monthTransactions = monthTransactions.Where(pt => pt.ProjectId == projectId.Value);
                monthReservations = monthReservations.Where(r => r.ProjectId == projectId.Value);
            }

            var transactionIncome = monthTransactions.Sum(pt => (decimal)pt.AmountPaid);
            var reservationIncome = monthReservations.Sum(r => (decimal)r.AmountPaid);
            var totalIncome = transactionIncome + reservationIncome;

            accumulated += totalIncome;

            var projectsCount = monthTransactions
                .Select(pt => (Guid?)pt.ProjectId)
                .Concat(monthReservations.Select(r => (Guid?)r.ProjectId))
                .Where(id => id != null)
                .Distinct()
                .Count();

            result.Add(
                new MonthlyIncomeDto
                {
                    Month = monthNames[month - 1],
                    Collected = totalIncome,
                    Accumulated = accumulated,
                    Projects = (int)projectsCount,
                }
            );
        }
        return result;
    }

    private List<PaymentScheduleDto> CalculatePaymentScheduleOptimized(
        IEnumerable<dynamic> payments,
        DateTime now
    )
    {
        var next30Days = now.AddDays(30);

        var upcomingPayments = payments
            .Where(p => !p.Paid && (DateTime)p.DueDate <= next30Days && (DateTime)p.DueDate >= now)
            .OrderBy(p => (DateTime)p.DueDate)
            .Take(10)
            .ToList();

        var result = new List<PaymentScheduleDto>();

        foreach (var payment in upcomingPayments)
        {
            var dueDate = (DateTime)payment.DueDate;
            var daysOverdue = (int)(now.Date - dueDate.Date).TotalDays;
            var status =
                daysOverdue > 0 ? "overdue"
                : daysOverdue <= 3 ? "due_soon"
                : "current";

            // Calcular cuota actual usando datos ya cargados
            var allPaymentsForReservation = payments
                .Where(p => p.ReservationId == payment.ReservationId)
                .OrderBy(p => (DateTime)p.DueDate)
                .ToList();

            var currentInstallment = allPaymentsForReservation.IndexOf(payment) + 1;
            var totalInstallments = (int)allPaymentsForReservation.Count;
            var installment = $"{currentInstallment}/{totalInstallments}";

            result.Add(
                new PaymentScheduleDto
                {
                    Client = payment.ClientName ?? "Cliente no encontrado",
                    Project = payment.ProjectName ?? "Proyecto no encontrado",
                    Amount = payment.AmountDue,
                    DueDate = dueDate.ToString("yyyy-MM-dd"),
                    DaysOverdue = Math.Max(0, (int)daysOverdue),
                    Status = status,
                    Installment = installment,
                    Lot = payment.LotNumber ?? "Lote no encontrado",
                }
            );
        }

        return result;
    }

    private List<DelinquencyAnalysisDto> CalculateDelinquencyAnalysisOptimized(
        IEnumerable<dynamic> payments,
        DateTime now
    )
    {
        var overduePayments = payments.Where(p => !p.Paid && (DateTime)p.DueDate < now).ToList();
        var totalOverdue = overduePayments.Sum(p => (decimal)p.AmountDue);

        var ranges = new[]
        {
            new
            {
                Range = "1-30 days",
                MinDays = 1,
                MaxDays = 30,
            },
            new
            {
                Range = "31-60 days",
                MinDays = 31,
                MaxDays = 60,
            },
            new
            {
                Range = "61-90 days",
                MinDays = 61,
                MaxDays = 90,
            },
            new
            {
                Range = "More than 90 days",
                MinDays = 91,
                MaxDays = int.MaxValue,
            },
        };

        var result = new List<DelinquencyAnalysisDto>();

        foreach (var range in ranges)
        {
            var rangePayments = overduePayments
                .Where(p =>
                {
                    var dueDate = (DateTime)p.DueDate;
                    var daysOverdue = (int)(now.Date - dueDate.Date).TotalDays;
                    return daysOverdue >= range.MinDays && daysOverdue <= range.MaxDays;
                })
                .ToList();

            var amount = rangePayments.Sum(p => (decimal)p.AmountDue);

            var quantity = (int)rangePayments.Count;

            var percentage =
                totalOverdue > 0 ? Math.Round((double)(amount / totalOverdue) * 100, 1) : 0;

            result.Add(
                new DelinquencyAnalysisDto
                {
                    Range = range.Range,
                    Amount = amount,
                    Quantity = (int)quantity,
                    Percentage = percentage,
                }
            );
        }

        return result;
    }

    private List<IncomeProjectionDto> CalculateIncomeProjectionOptimized(
        IEnumerable<dynamic> payments,
        DateTime now
    )
    {
        var result = new List<IncomeProjectionDto>();

        // Obtener los próximos meses que tienen pagos programados
        var futurePayments = payments.Where(p => !p.Paid && (DateTime)p.DueDate > now);

        if (!futurePayments.Any())
        {
            return result; // No hay pagos futuros
        }

        // Agrupar por mes los pagos futuros
        var paymentsByMonth = futurePayments
            .GroupBy(p => new
            {
                Year = ((DateTime)p.DueDate).Year,
                Month = ((DateTime)p.DueDate).Month,
            })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Month)
            .Take(6); // Máximo 6 meses de proyección

        var monthNames = new[]
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

        foreach (var monthGroup in paymentsByMonth)
        {
            var monthPayments = monthGroup.Sum(p => (decimal)p.AmountDue);

            // Proyecciones basadas en datos históricos
            var conservative = monthPayments * 0.85m;
            var realistic = monthPayments;
            var optimistic = monthPayments * 1.15m;

            result.Add(
                new IncomeProjectionDto
                {
                    Month = monthNames[monthGroup.Key.Month - 1],
                    Conservative = conservative,
                    Realistic = realistic,
                    Optimistic = optimistic,
                }
            );
        }

        return result;
    }

    private double CalculatePortfolioDaysOptimized(IEnumerable<dynamic> payments, DateTime now)
    {
        var totalPending = payments.Where(p => !p.Paid).Sum(p => (decimal)p.AmountDue);
        var totalOverdue = payments
            .Where(p => !p.Paid && (DateTime)p.DueDate < now)
            .Sum(p => (decimal)p.AmountDue);

        if (totalPending == 0)
            return 0;

        // Cálculo simplificado de días de cartera
        return Math.Round((double)(totalOverdue / totalPending) * 30, 0);
    }

    private double CalculateMonthlyGrowthOptimized(List<MonthlyIncomeDto> monthlyIncome)
    {
        if (monthlyIncome.Count < 2)
            return 0;

        var currentMonth = monthlyIncome.Last().Collected;
        var previousMonth = monthlyIncome[monthlyIncome.Count - 2].Collected;

        if (previousMonth == 0)
            return 0;

        return Math.Round(
            ((double)(currentMonth - previousMonth) / (double)previousMonth) * 100,
            1
        );
    }
}
