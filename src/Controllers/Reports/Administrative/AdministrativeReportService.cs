using GestionHogar.Controllers.Reports;
using GestionHogar.Controllers.Reports.Dto;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Services;

public class AdministrativeReportService : IAdministrativeReportService
{
    private readonly DatabaseContext _context;

    public AdministrativeReportService(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<PaginatedResponseV2<CreditsReportItemDto>> GetCreditsReportPaginatedAsync(
        DateOnly from,
        DateOnly to,
        int page,
        int pageSize,
        PaginationService paginationService,
        string? search = null,
        Guid? projectId = null,
        string? pendingInstallments = null,
        string? orderBy = null
    )
    {
        var query = ProjectToCreditItems(
            BuildCreditsQuery(from, to, search, projectId, pendingInstallments)
        );
        query = ApplyCreditsOrdering(query, orderBy);

        return await paginationService.PaginateAsync(query, page, pageSize);
    }

    public async Task<byte[]> ExportCreditsReportAsync(
        DateOnly from,
        DateOnly to,
        IExcelExportService excelExportService,
        string? search = null,
        Guid? projectId = null,
        string? pendingInstallments = null
    )
    {
        var items = await ProjectToCreditItems(
                BuildCreditsQuery(from, to, search, projectId, pendingInstallments)
            )
            .OrderByDescending(r => r.StartDate)
            .ToListAsync();

        var originLabels = new Dictionary<string, string>
        {
            { "Pipeline", "Separación" },
            { "Direct", "Directo" },
            { "Legacy", "Legado" },
        };

        var headers = new List<string>
        {
            "Código",
            "Cliente",
            "DNI / RUC",
            "Proyecto",
            "Manzana",
            "Lote",
            "Fecha de inicio",
            "Origen",
            "Moneda",
            "Inicial",
            "Monto pagado",
            "Monto restante",
            "Total venta",
            "Cuotas pendientes",
            "Siguiente pago",
            "Monto cuota",
            "Meses financiados",
        };

        var data = items
            .Select(item =>
                new List<object>
                {
                    item.Code,
                    item.ClientName,
                    item.ClientDni ?? "",
                    item.ProjectName,
                    item.BlockName,
                    item.LotNumber,
                    item.StartDate.ToString("dd/MM/yyyy"),
                    originLabels.GetValueOrDefault(item.Origin.ToString(), item.Origin.ToString()),
                    item.Currency == Currency.DOLARES ? "US$" : "S/",
                    item.DownPaymentAmount,
                    item.AmountPaid,
                    item.RemainingAmount,
                    item.TotalPrice,
                    item.PendingInstallmentCount,
                    item.NextPaymentDueDate.HasValue
                        ? item.NextPaymentDueDate.Value.ToString("dd/MM/yyyy")
                        : "—",
                    item.NextPaymentAmount,
                    item.MonthsFinanced,
                }
            )
            .ToList();

        return excelExportService.GenerateExcel("Reporte Administrativo - Créditos", headers, data);
    }

    public async Task<CreditStatementPdfFile> GenerateCreditStatementPdfAsync(Guid contractId)
    {
        var contract =
            await _context
                .Contracts.Include(c => c.Client)
                .Include(c => c.Lot)
                .ThenInclude(l => l.Block)
                .ThenInclude(b => b.Project)
                .Include(c => c.Payments)
                .FirstOrDefaultAsync(c => c.Id == contractId && c.IsActive)
            ?? throw new ArgumentException("Contrato no encontrado");

        var payments = contract
            .Payments.Where(p => p.IsActive)
            .OrderBy(p => p.Sequence)
            .ThenBy(p => p.DueDate)
            .ToList();
        var paymentIds = payments.Select(p => p.Id).ToList();
        var paidByPayment = await _context
            .PaymentTransactionPayments.Where(ptp => paymentIds.Contains(ptp.PaymentId))
            .GroupBy(ptp => ptp.PaymentId)
            .Select(g => new { PaymentId = g.Key, AmountPaid = g.Sum(x => x.AmountPaid) })
            .ToDictionaryAsync(x => x.PaymentId, x => x.AmountPaid);

        var quotas = payments
            .Select(payment =>
            {
                var amountPaid = paidByPayment.GetValueOrDefault(payment.Id, 0);
                if (payment.Paid)
                {
                    amountPaid = Math.Max(amountPaid, payment.AmountDue);
                }

                var remaining = Math.Max(0, payment.AmountDue - amountPaid);
                return new CreditStatementQuota
                {
                    Payment = payment,
                    AmountPaid = amountPaid,
                    RemainingAmount = remaining,
                    Paid = remaining <= 0,
                };
            })
            .ToList();

        var transactions = await _context
            .PaymentTransactions.Include(pt => pt.PaymentDetails)
            .Include(pt => pt.Payments)
            .Where(pt => pt.IsActive && pt.ContractId == contractId)
            .OrderBy(pt => pt.PaymentDate)
            .ToListAsync();

        var bytes = CreditStatementPdf.Generate(
            new CreditStatementPdfModel
            {
                Contract = contract,
                Quotas = quotas,
                Transactions = transactions,
            }
        );

        var safeCode = string.IsNullOrWhiteSpace(contract.Code) ? contract.Id.ToString("N") : contract.Code;
        return new CreditStatementPdfFile
        {
            Bytes = bytes,
            FileName = $"estado-cuenta-{safeCode}.pdf",
        };
    }

    public async Task<IncomeReportResponse> GetIncomeReportPaginatedAsync(
        DateOnly from,
        DateOnly to,
        int page,
        int pageSize,
        PaginationService paginationService,
        Guid? projectId = null,
        string? orderBy = null
    )
    {
        var baseQuery = BuildIncomeQuery(from, to, projectId);
        var summary = await BuildIncomeSummaryAsync(baseQuery);

        var itemsQuery = ApplyIncomeOrdering(ProjectToIncomeItems(baseQuery), orderBy);
        var paginated = await paginationService.PaginateAsync(itemsQuery, page, pageSize);

        return new IncomeReportResponse
        {
            Data = paginated.Data,
            Meta = paginated.Meta,
            Summary = summary,
        };
    }

    public async Task<byte[]> ExportIncomeReportAsync(
        DateOnly from,
        DateOnly to,
        IExcelExportService excelExportService,
        Guid? projectId = null
    )
    {
        var items = await ProjectToIncomeItems(BuildIncomeQuery(from, to, projectId))
            .OrderByDescending(r => r.PaymentDate)
            .ToListAsync();

        var methodLabels = new Dictionary<string, string>
        {
            { "CASH", "Efectivo" },
            { "BANK_DEPOSIT", "Depósito bancario" },
            { "BANK_TRANSFER", "Transferencia bancaria" },
        };

        var headers = new List<string>
        {
            "Fecha de pago",
            "Monto",
            "Moneda",
            "Método",
            "Nº operación",
            "Cliente",
            "DNI / RUC",
            "Proyecto",
            "Origen",
            "Código contrato",
            "Cuotas cubiertas",
        };

        var data = items
            .Select(item =>
                new List<object>
                {
                    item.PaymentDate.ToOffset(TimeSpan.FromHours(-5)).ToString("dd/MM/yyyy"),
                    item.AmountPaid,
                    item.Currency == Currency.DOLARES ? "US$" : "S/",
                    methodLabels.GetValueOrDefault(
                        item.PaymentMethod.ToString(),
                        item.PaymentMethod.ToString()
                    ),
                    item.ReferenceNumber ?? "",
                    item.ClientName,
                    item.ClientDni ?? "",
                    item.ProjectName ?? "Sin proyecto",
                    item.Origin == IncomeReportOrigin.Reservation ? "Separación" : "Contrato",
                    item.ContractCode ?? "",
                    item.QuotasCovered,
                }
            )
            .ToList();

        return excelExportService.GenerateExcel("Reporte Administrativo - Ingresos", headers, data);
    }

    private IQueryable<Contract> BuildCreditsQuery(
        DateOnly from,
        DateOnly to,
        string? search,
        Guid? projectId,
        string? pendingInstallments
    )
    {
        var rangeStart = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var rangeEndExclusive = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var query = _context.Contracts.Where(c =>
            c.IsActive
            && c.Status == ContractStatus.Active
            && c.StartDate >= rangeStart
            && c.StartDate < rangeEndExclusive
        );

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c =>
                c.Code.ToLower().Contains(term)
                || (c.Client.Name != null && c.Client.Name.ToLower().Contains(term))
                || (c.Client.CompanyName != null && c.Client.CompanyName.ToLower().Contains(term))
                || (c.Client.Dni != null && c.Client.Dni.Contains(term))
                || (c.Client.Ruc != null && c.Client.Ruc.Contains(term))
            );
        }

        if (projectId.HasValue)
        {
            query = query.Where(c => c.Lot.Block.ProjectId == projectId.Value);
        }

        var pendingFilter = (pendingInstallments ?? "all").Trim().ToLowerInvariant();
        if (pendingFilter == "pending")
        {
            query = query.Where(c => c.Payments.Any(p => p.IsActive && !p.Paid));
        }
        else if (pendingFilter == "overdue")
        {
            var now = DateTime.UtcNow;
            query = query.Where(c =>
                c.Payments.Any(p => p.IsActive && !p.Paid && p.DueDate < now)
            );
        }

        return query;
    }

    private static IQueryable<CreditsReportItemDto> ProjectToCreditItems(IQueryable<Contract> query)
    {
        return query.Select(c => new CreditsReportItemDto
        {
            Id = c.Id,
            Code = c.Code,
            ClientId = c.ClientId,
            ClientName =
                c.Client.Type == ClientType.Natural
                    ? c.Client.Name ?? string.Empty
                    : c.Client.CompanyName ?? c.Client.Name ?? string.Empty,
            ClientDni = c.Client.Dni ?? c.Client.Ruc,
            ProjectId = c.Lot.Block.ProjectId,
            ProjectName = c.Lot.Block.Project.Name,
            LotNumber = c.Lot.LotNumber,
            BlockName = c.Lot.Block.Name,
            StartDate = c.StartDate,
            Currency = c.Currency,
            TotalPrice = c.TotalPrice,
            DownPaymentAmount = c.DownPaymentAmount,
            AmountPaid = c.Payments.Where(p => p.IsActive && p.Paid).Sum(p => p.AmountDue),
            RemainingAmount = c.Payments.Where(p => p.IsActive && !p.Paid).Sum(p => p.AmountDue),
            MonthsFinanced = c.MonthsFinanced,
            PendingInstallmentCount = c.Payments.Count(p => p.IsActive && !p.Paid),
            NextPaymentDueDate = c
                .Payments.Where(p => p.IsActive && !p.Paid)
                .OrderBy(p => p.DueDate)
                .Select(p => (DateTime?)p.DueDate)
                .FirstOrDefault(),
            NextPaymentAmount = c
                .Payments.Where(p => p.IsActive && !p.Paid)
                .OrderBy(p => p.DueDate)
                .Select(p => p.AmountDue)
                .FirstOrDefault(),
            Origin = c.Origin,
        });
    }

    private static IQueryable<CreditsReportItemDto> ApplyCreditsOrdering(
        IQueryable<CreditsReportItemDto> query,
        string? orderBy
    )
    {
        if (string.IsNullOrWhiteSpace(orderBy))
        {
            return query.OrderByDescending(r => r.StartDate);
        }

        var orderParts = orderBy.Split(' ');
        var field = orderParts[0].ToLower();
        var direction = orderParts.Length > 1 && orderParts[1].ToLower() == "desc" ? "desc" : "asc";

        return field switch
        {
            "code" => direction == "desc"
                ? query.OrderByDescending(r => r.Code)
                : query.OrderBy(r => r.Code),
            "clientname" => direction == "desc"
                ? query.OrderByDescending(r => r.ClientName)
                : query.OrderBy(r => r.ClientName),
            "projectname" => direction == "desc"
                ? query.OrderByDescending(r => r.ProjectName)
                : query.OrderBy(r => r.ProjectName),
            "remainingamount" => direction == "desc"
                ? query.OrderByDescending(r => r.RemainingAmount)
                : query.OrderBy(r => r.RemainingAmount),
            "amountpaid" => direction == "desc"
                ? query.OrderByDescending(r => r.AmountPaid)
                : query.OrderBy(r => r.AmountPaid),
            "pendinginstallmentcount" => direction == "desc"
                ? query.OrderByDescending(r => r.PendingInstallmentCount)
                : query.OrderBy(r => r.PendingInstallmentCount),
            "nextpaymentduedate" => direction == "desc"
                ? query.OrderByDescending(r => r.NextPaymentDueDate)
                : query.OrderBy(r => r.NextPaymentDueDate),
            "startdate" => direction == "desc"
                ? query.OrderByDescending(r => r.StartDate)
                : query.OrderBy(r => r.StartDate),
            _ => query.OrderByDescending(r => r.StartDate),
        };
    }

    private IQueryable<PaymentTransaction> BuildIncomeQuery(
        DateOnly from,
        DateOnly to,
        Guid? projectId
    )
    {
        var rangeStart = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var rangeEndExclusive = new DateTimeOffset(
            to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        );

        var query = _context.PaymentTransactions.Where(pt =>
            pt.IsActive && pt.PaymentDate >= rangeStart && pt.PaymentDate < rangeEndExclusive
        );

        if (projectId.HasValue)
        {
            query = query.Where(pt =>
                (
                    pt.Contract != null
                    && pt.Contract.Lot.Block.ProjectId == projectId.Value
                )
                || (
                    pt.Contract == null
                    && pt.Reservation != null
                    && pt.Reservation.Quotation.Lot!.Block.ProjectId == projectId.Value
                )
            );
        }

        return query;
    }

    private static IQueryable<IncomeReportItemDto> ProjectToIncomeItems(
        IQueryable<PaymentTransaction> query
    )
    {
        return query.Select(pt => new IncomeReportItemDto
        {
            Id = pt.Id,
            PaymentDate = pt.PaymentDate,
            AmountPaid = pt.AmountPaid,
            Currency =
                pt.Contract != null
                    ? pt.Contract.Currency
                    : pt.Reservation != null
                        ? pt.Reservation.Currency
                        : Currency.SOLES,
            PaymentMethod = pt.PaymentMethod,
            ReferenceNumber = pt.ReferenceNumber,
            ClientId =
                pt.Contract != null
                    ? pt.Contract.ClientId
                    : pt.Reservation != null
                        ? pt.Reservation.ClientId
                        : null,
            ClientName =
                pt.Contract != null
                    ? (
                        pt.Contract.Client.Type == ClientType.Natural
                            ? pt.Contract.Client.Name ?? string.Empty
                            : pt.Contract.Client.CompanyName
                                ?? pt.Contract.Client.Name
                                ?? string.Empty
                    )
                    : pt.Reservation != null
                        ? (
                            pt.Reservation.Client.Type == ClientType.Natural
                                ? pt.Reservation.Client.Name ?? string.Empty
                                : pt.Reservation.Client.CompanyName
                                    ?? pt.Reservation.Client.Name
                                    ?? string.Empty
                        )
                        : string.Empty,
            ClientDni =
                pt.Contract != null
                    ? pt.Contract.Client.Dni ?? pt.Contract.Client.Ruc
                    : pt.Reservation != null
                        ? pt.Reservation.Client.Dni ?? pt.Reservation.Client.Ruc
                        : null,
            ProjectId =
                pt.Contract != null
                    ? pt.Contract.Lot.Block.ProjectId
                    : pt.Reservation != null
                        ? pt.Reservation.Quotation.Lot!.Block.ProjectId
                        : null,
            ProjectName =
                pt.Contract != null
                    ? pt.Contract.Lot.Block.Project.Name
                    : pt.Reservation != null
                        ? pt.Reservation.Quotation.Lot!.Block.Project.Name
                        : null,
            ContractId = pt.ContractId,
            ContractCode = pt.Contract != null ? pt.Contract.Code : null,
            ReservationId = pt.ReservationId,
            Origin =
                pt.ContractId == null
                    ? IncomeReportOrigin.Reservation
                    : IncomeReportOrigin.Contract,
            QuotasCovered =
                pt.PaymentDetails.Count > 0 ? pt.PaymentDetails.Count : pt.Payments.Count,
        });
    }

    private static IQueryable<IncomeReportItemDto> ApplyIncomeOrdering(
        IQueryable<IncomeReportItemDto> query,
        string? orderBy
    )
    {
        if (string.IsNullOrWhiteSpace(orderBy))
        {
            return query.OrderByDescending(r => r.PaymentDate);
        }

        var orderParts = orderBy.Split(' ');
        var field = orderParts[0].ToLower();
        var direction = orderParts.Length > 1 && orderParts[1].ToLower() == "desc" ? "desc" : "asc";

        return field switch
        {
            "amountpaid" => direction == "desc"
                ? query.OrderByDescending(r => r.AmountPaid)
                : query.OrderBy(r => r.AmountPaid),
            "clientname" => direction == "desc"
                ? query.OrderByDescending(r => r.ClientName)
                : query.OrderBy(r => r.ClientName),
            "projectname" => direction == "desc"
                ? query.OrderByDescending(r => r.ProjectName)
                : query.OrderBy(r => r.ProjectName),
            "paymentmethod" => direction == "desc"
                ? query.OrderByDescending(r => r.PaymentMethod)
                : query.OrderBy(r => r.PaymentMethod),
            "paymentdate" => direction == "desc"
                ? query.OrderByDescending(r => r.PaymentDate)
                : query.OrderBy(r => r.PaymentDate),
            _ => query.OrderByDescending(r => r.PaymentDate),
        };
    }

    private static async Task<IncomeReportSummaryDto> BuildIncomeSummaryAsync(
        IQueryable<PaymentTransaction> query
    )
    {
        var rows = await query
            .Select(pt => new
            {
                pt.AmountPaid,
                IsReservation = pt.ContractId == null,
                Currency = pt.Contract != null
                    ? pt.Contract.Currency
                    : pt.Reservation != null
                        ? pt.Reservation.Currency
                        : Currency.SOLES,
            })
            .ToListAsync();

        var totalsByCurrency = rows.GroupBy(r => r.Currency)
            .Select(g => new IncomeReportCurrencyTotalDto
            {
                Currency = g.Key,
                PaymentCount = g.Count(),
                TotalAmount = g.Sum(x => x.AmountPaid),
                ReservationPaymentCount = g.Count(x => x.IsReservation),
                ReservationPaymentAmount = g.Where(x => x.IsReservation).Sum(x => x.AmountPaid),
            })
            .OrderBy(t => t.Currency)
            .ToList();

        return new IncomeReportSummaryDto
        {
            PaymentCount = rows.Count,
            TotalAmount = rows.Sum(r => r.AmountPaid),
            ReservationPaymentCount = rows.Count(r => r.IsReservation),
            ReservationPaymentAmount = rows.Where(r => r.IsReservation).Sum(r => r.AmountPaid),
            TotalsByCurrency = totalsByCurrency,
        };
    }
}
