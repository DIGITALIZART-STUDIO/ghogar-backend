using GestionHogar.Model;
using GestionHogar.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionHogar.Tests.Services;

public class AdministrativeReportServiceTests
{
    [Fact]
    public async Task CreditsReport_OnlyIncludesActiveContractsInStartDateRange()
    {
        var context = CreateContext();
        var seed = await SeedCreditsAsync(context);
        var service = new AdministrativeReportService(context);
        var pagination = new PaginationService();

        var result = await service.GetCreditsReportPaginatedAsync(
            DateOnly.FromDateTime(seed.InRangeStart),
            DateOnly.FromDateTime(seed.InRangeStart),
            page: 1,
            pageSize: 10,
            pagination
        );

        Assert.Single(result.Data);
        Assert.Equal(seed.ActiveInRange.Id, result.Data[0].Id);
        Assert.Equal("12345678", result.Data[0].ClientDni);
        Assert.Equal("Proyecto Norte", result.Data[0].ProjectName);
        Assert.True(result.Data[0].PendingInstallmentCount > 0);
    }

    [Fact]
    public async Task CreditsReport_FiltersByClientDniAndPendingInstallments()
    {
        var context = CreateContext();
        var seed = await SeedCreditsAsync(context);
        var service = new AdministrativeReportService(context);
        var pagination = new PaginationService();

        var byDni = await service.GetCreditsReportPaginatedAsync(
            DateOnly.FromDateTime(seed.InRangeStart.AddDays(-1)),
            DateOnly.FromDateTime(seed.InRangeStart.AddDays(1)),
            page: 1,
            pageSize: 10,
            pagination,
            search: "12345678"
        );

        Assert.Single(byDni.Data);
        Assert.Equal(seed.ActiveInRange.Code, byDni.Data[0].Code);

        foreach (var payment in context.Payments.Where(p => p.ContractId == seed.ActiveInRange.Id))
        {
            payment.Paid = true;
        }
        await context.SaveChangesAsync();

        var pendingOnly = await service.GetCreditsReportPaginatedAsync(
            DateOnly.FromDateTime(seed.InRangeStart.AddDays(-1)),
            DateOnly.FromDateTime(seed.InRangeStart.AddDays(1)),
            page: 1,
            pageSize: 10,
            pagination,
            pendingInstallments: "pending"
        );

        Assert.Empty(pendingOnly.Data);
    }

    [Fact]
    public async Task CreditsReport_PaginatesAndExportsFilteredRows()
    {
        var context = CreateContext();
        var seed = await SeedCreditsAsync(context);
        var service = new AdministrativeReportService(context);
        var pagination = new PaginationService();
        var excel = new FakeExcelExportService();

        var page = await service.GetCreditsReportPaginatedAsync(
            DateOnly.FromDateTime(seed.InRangeStart.AddDays(-1)),
            DateOnly.FromDateTime(seed.InRangeStart.AddDays(1)),
            page: 1,
            pageSize: 1,
            pagination
        );

        Assert.Equal(1, page.Meta.PageSize);
        Assert.Single(page.Data);

        var bytes = await service.ExportCreditsReportAsync(
            DateOnly.FromDateTime(seed.InRangeStart),
            DateOnly.FromDateTime(seed.InRangeStart),
            excel,
            search: "Ana"
        );

        Assert.Equal(new byte[] { 1, 2, 3 }, bytes);
        Assert.Contains("Código", excel.LastHeaders!);
        Assert.Single(excel.LastData!);
        Assert.Equal(seed.ActiveInRange.Code, excel.LastData![0][0]);
    }

    [Fact]
    public async Task CreditStatementPdf_IncludesClientScheduleAndRegisteredPayments()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var context = CreateContext();
        var seed = await SeedCreditsAsync(context);
        context.PaymentTransactions.Add(
            new PaymentTransaction
            {
                PaymentDate = new DateTimeOffset(seed.InRangeStart),
                AmountPaid = 1500,
                Contract = seed.ActiveInRange,
                PaymentMethod = PaymentMethod.CASH,
                ReferenceNumber = "OP-PDF",
            }
        );
        await context.SaveChangesAsync();

        var service = new AdministrativeReportService(context);
        var file = await service.GenerateCreditStatementPdfAsync(seed.ActiveInRange.Id);

        Assert.StartsWith("estado-cuenta-", file.FileName);
        Assert.EndsWith(".pdf", file.FileName);
        Assert.True(file.Bytes.Length > 200);
    }

    [Fact]
    public async Task IncomeReport_IncludesContractAndReservationPaymentsWithSummary()
    {
        var context = CreateContext();
        var seed = await SeedIncomeAsync(context);
        var service = new AdministrativeReportService(context);
        var pagination = new PaginationService();

        var result = await service.GetIncomeReportPaginatedAsync(
            DateOnly.FromDateTime(seed.PaymentDay),
            DateOnly.FromDateTime(seed.PaymentDay),
            page: 1,
            pageSize: 10,
            pagination
        );

        Assert.Equal(2, result.Meta.Total);
        Assert.Equal(2, result.Summary.PaymentCount);
        Assert.Equal(1500m, result.Summary.TotalAmount);
        Assert.Equal(1, result.Summary.ReservationPaymentCount);
        Assert.Equal(500m, result.Summary.ReservationPaymentAmount);
        Assert.Contains(result.Data, item => item.Origin == GestionHogar.Controllers.Reports.Dto.IncomeReportOrigin.Reservation);
        Assert.Contains(result.Data, item => item.Origin == GestionHogar.Controllers.Reports.Dto.IncomeReportOrigin.Contract);
    }

    [Fact]
    public async Task IncomeReport_FiltersByProjectAndPaginates()
    {
        var context = CreateContext();
        var seed = await SeedIncomeAsync(context);
        var service = new AdministrativeReportService(context);
        var pagination = new PaginationService();

        var filtered = await service.GetIncomeReportPaginatedAsync(
            DateOnly.FromDateTime(seed.PaymentDay),
            DateOnly.FromDateTime(seed.PaymentDay),
            page: 1,
            pageSize: 1,
            pagination,
            projectId: seed.Project.Id
        );

        Assert.Equal(2, filtered.Meta.Total);
        Assert.Single(filtered.Data);
        Assert.Equal(2, filtered.Meta.TotalPages);

        var otherProject = await service.GetIncomeReportPaginatedAsync(
            DateOnly.FromDateTime(seed.PaymentDay),
            DateOnly.FromDateTime(seed.PaymentDay),
            page: 1,
            pageSize: 10,
            pagination,
            projectId: Guid.NewGuid()
        );

        Assert.Empty(otherProject.Data);
        Assert.Equal(0, otherProject.Summary.PaymentCount);
    }

    [Fact]
    public async Task CreditsReport_FiltersByProjectAndComputesBalances()
    {
        var context = CreateContext();
        var seed = await SeedCreditsAsync(context);
        var paidInstallment = context.Payments.Single(p => p.ContractId == seed.ActiveInRange.Id);
        paidInstallment.Paid = true;
        context.Payments.Add(
            new Payment
            {
                Contract = seed.ActiveInRange,
                Type = PaymentType.Installment,
                Sequence = 2,
                DueDate = seed.InRangeStart.AddMonths(2),
                AmountDue = 2500,
                Paid = false,
            }
        );
        await context.SaveChangesAsync();

        var service = new AdministrativeReportService(context);
        var pagination = new PaginationService();

        var byProject = await service.GetCreditsReportPaginatedAsync(
            DateOnly.FromDateTime(seed.InRangeStart),
            DateOnly.FromDateTime(seed.InRangeStart),
            page: 1,
            pageSize: 10,
            pagination,
            projectId: seed.ActiveInRange.Lot.Block.Project.Id
        );

        Assert.Single(byProject.Data);
        Assert.Equal(3000, byProject.Data[0].AmountPaid);
        Assert.Equal(2500, byProject.Data[0].RemainingAmount);
        Assert.Equal(1, byProject.Data[0].PendingInstallmentCount);
        Assert.Equal(2500, byProject.Data[0].NextPaymentAmount);

        var otherProject = await service.GetCreditsReportPaginatedAsync(
            DateOnly.FromDateTime(seed.InRangeStart),
            DateOnly.FromDateTime(seed.InRangeStart),
            page: 1,
            pageSize: 10,
            pagination,
            projectId: Guid.NewGuid()
        );

        Assert.Empty(otherProject.Data);
    }

    [Fact]
    public async Task CreditsReport_FiltersOverdueInstallmentsAndOrdersByCode()
    {
        var context = CreateContext();
        var seed = await SeedCreditsAsync(context);
        var overduePayment = context.Payments.Single(p => p.ContractId == seed.ActiveInRange.Id);
        overduePayment.DueDate = DateTime.UtcNow.AddDays(-5);
        var futureLot = new Lot
        {
            LotNumber = "20",
            Area = 90,
            Price = 40000,
            Status = LotStatus.Sold,
            Block = seed.ActiveInRange.Lot.Block,
        };
        var futureContract = new Contract
        {
            Code = "CNT-2026-00010",
            Client = seed.ActiveInRange.Client,
            Lot = futureLot,
            Origin = ContractOrigin.Direct,
            Status = ContractStatus.Active,
            TotalPrice = 40000,
            DownPaymentAmount = 4000,
            AmountFinanced = 36000,
            MonthsFinanced = 12,
            Currency = Currency.SOLES,
            ExchangeRate = 1,
            StartDate = seed.InRangeStart,
        };
        context.Lots.Add(futureLot);
        context.Contracts.Add(futureContract);
        context.Payments.Add(
            new Payment
            {
                Contract = futureContract,
                Type = PaymentType.Installment,
                Sequence = 1,
                DueDate = DateTime.UtcNow.AddDays(10),
                AmountDue = 3000,
                Paid = false,
            }
        );
        await context.SaveChangesAsync();

        var service = new AdministrativeReportService(context);
        var pagination = new PaginationService();
        var from = DateOnly.FromDateTime(seed.InRangeStart);
        var to = DateOnly.FromDateTime(seed.InRangeStart);

        var overdue = await service.GetCreditsReportPaginatedAsync(
            from,
            to,
            page: 1,
            pageSize: 10,
            pagination,
            pendingInstallments: "overdue"
        );

        Assert.Single(overdue.Data);
        Assert.Equal(seed.ActiveInRange.Code, overdue.Data[0].Code);

        var pending = await service.GetCreditsReportPaginatedAsync(
            from,
            to,
            page: 1,
            pageSize: 10,
            pagination,
            pendingInstallments: "pending"
        );

        Assert.Equal(2, pending.Meta.Total);

        var ordered = await service.GetCreditsReportPaginatedAsync(
            from,
            to,
            page: 1,
            pageSize: 10,
            pagination,
            orderBy: "code asc"
        );

        Assert.Equal(seed.ActiveInRange.Code, ordered.Data[0].Code);
        Assert.Equal(futureContract.Code, ordered.Data[1].Code);
    }

    [Fact]
    public async Task CreditsReport_ExcludesInactiveContracts()
    {
        var context = CreateContext();
        var seed = await SeedCreditsAsync(context);
        seed.ActiveInRange.IsActive = false;
        await context.SaveChangesAsync();

        var service = new AdministrativeReportService(context);
        var pagination = new PaginationService();

        var result = await service.GetCreditsReportPaginatedAsync(
            DateOnly.FromDateTime(seed.InRangeStart),
            DateOnly.FromDateTime(seed.InRangeStart),
            page: 1,
            pageSize: 10,
            pagination
        );

        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task CreditStatementPdf_ThrowsWhenContractIsMissingOrInactive()
    {
        var context = CreateContext();
        var seed = await SeedCreditsAsync(context);
        var service = new AdministrativeReportService(context);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GenerateCreditStatementPdfAsync(Guid.NewGuid())
        );

        seed.ActiveInRange.IsActive = false;
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GenerateCreditStatementPdfAsync(seed.ActiveInRange.Id)
        );
    }

    [Fact]
    public async Task CreditStatementPdf_IncludesPartialPaymentDetails()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var context = CreateContext();
        var seed = await SeedCreditsAsync(context);
        var installment = context.Payments.Single(p => p.ContractId == seed.ActiveInRange.Id);
        var transaction = new PaymentTransaction
        {
            PaymentDate = new DateTimeOffset(seed.InRangeStart),
            AmountPaid = 500,
            Contract = seed.ActiveInRange,
            PaymentMethod = PaymentMethod.BANK_DEPOSIT,
            ReferenceNumber = "PARCIAL-1",
        };
        context.PaymentTransactions.Add(transaction);
        await context.SaveChangesAsync();
        context.PaymentTransactionPayments.Add(
            new PaymentTransactionPayment
            {
                PaymentTransactionId = transaction.Id,
                PaymentId = installment.Id,
                AmountPaid = 500,
            }
        );
        await context.SaveChangesAsync();

        var service = new AdministrativeReportService(context);
        var file = await service.GenerateCreditStatementPdfAsync(seed.ActiveInRange.Id);

        Assert.Equal($"estado-cuenta-{seed.ActiveInRange.Code}.pdf", file.FileName);
        Assert.True(file.Bytes.Length > 200);
    }

    [Fact]
    public async Task IncomeReport_ExportsFilteredRowsAndIgnoresInactiveTransactions()
    {
        var context = CreateContext();
        var seed = await SeedIncomeAsync(context);
        context.PaymentTransactions.Add(
            new PaymentTransaction
            {
                PaymentDate = new DateTimeOffset(seed.PaymentDay),
                AmountPaid = 777,
                Contract = context.Contracts.Single(c => c.Code == "CNT-2026-01000"),
                PaymentMethod = PaymentMethod.CASH,
                ReferenceNumber = "INACTIVE",
                IsActive = false,
            }
        );
        await context.SaveChangesAsync();

        var service = new AdministrativeReportService(context);
        var excel = new FakeExcelExportService();

        var bytes = await service.ExportIncomeReportAsync(
            DateOnly.FromDateTime(seed.PaymentDay),
            DateOnly.FromDateTime(seed.PaymentDay),
            excel,
            projectId: seed.Project.Id
        );

        Assert.Equal(new byte[] { 1, 2, 3 }, bytes);
        Assert.Contains("Fecha de pago", excel.LastHeaders!);
        Assert.Equal(2, excel.LastData!.Count);
        Assert.DoesNotContain(excel.LastData, row => row.Contains("INACTIVE"));
    }

    [Fact]
    public async Task IncomeReport_GroupsSummaryByCurrencyAndOrdersByAmount()
    {
        var context = CreateContext();
        var seed = await SeedIncomeAsync(context);
        var contract = context.Contracts.Single(c => c.Code == "CNT-2026-01000");
        contract.Currency = Currency.DOLARES;
        var dollarPayment = context.PaymentTransactions.Single(pt =>
            pt.ContractId == contract.Id && pt.ReferenceNumber == "OP-100"
        );
        dollarPayment.AmountPaid = 200;
        await context.SaveChangesAsync();

        var service = new AdministrativeReportService(context);
        var pagination = new PaginationService();

        var result = await service.GetIncomeReportPaginatedAsync(
            DateOnly.FromDateTime(seed.PaymentDay),
            DateOnly.FromDateTime(seed.PaymentDay),
            page: 1,
            pageSize: 10,
            pagination,
            orderBy: "amountpaid asc"
        );

        Assert.Equal(2, result.Summary.PaymentCount);
        Assert.Equal(700m, result.Summary.TotalAmount);
        Assert.Equal(2, result.Summary.TotalsByCurrency.Count);
        Assert.Contains(
            result.Summary.TotalsByCurrency,
            t => t.Currency == Currency.DOLARES && t.TotalAmount == 200m && t.PaymentCount == 1
        );
        Assert.Contains(
            result.Summary.TotalsByCurrency,
            t =>
                t.Currency == Currency.SOLES
                && t.TotalAmount == 500m
                && t.ReservationPaymentCount == 1
        );
        Assert.Equal(200m, result.Data[0].AmountPaid);
        Assert.Equal(500m, result.Data[1].AmountPaid);
    }

    private static DatabaseContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DatabaseContext(options);
    }

    private static async Task<CreditsSeed> SeedCreditsAsync(DatabaseContext context)
    {
        var inRangeStart = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var client = new Client
        {
            Name = "Ana Ruiz",
            Dni = "12345678",
            PhoneNumber = "999111000",
            Type = ClientType.Natural,
        };
        var project = new Project
        {
            Name = "Proyecto Norte",
            Location = "Lima",
            Currency = "PEN",
        };
        var block = new Block { Name = "A", Project = project };
        var lot = new Lot
        {
            LotNumber = "12",
            Area = 90,
            Price = 40000,
            Status = LotStatus.Sold,
            Block = block,
        };
        var otherLot = new Lot
        {
            LotNumber = "13",
            Area = 90,
            Price = 40000,
            Status = LotStatus.Available,
            Block = block,
        };

        var activeInRange = new Contract
        {
            Code = "CNT-2026-00001",
            Client = client,
            Lot = lot,
            Origin = ContractOrigin.Direct,
            Status = ContractStatus.Active,
            TotalPrice = 40000,
            DownPaymentAmount = 4000,
            AmountFinanced = 36000,
            MonthsFinanced = 12,
            Currency = Currency.SOLES,
            ExchangeRate = 1,
            StartDate = inRangeStart,
        };
        var pendingInRange = new Contract
        {
            Code = "CNT-2026-00002",
            Client = client,
            Lot = otherLot,
            Origin = ContractOrigin.Direct,
            Status = ContractStatus.PendingValidation,
            TotalPrice = 40000,
            DownPaymentAmount = 4000,
            AmountFinanced = 36000,
            MonthsFinanced = 12,
            Currency = Currency.SOLES,
            ExchangeRate = 1,
            StartDate = inRangeStart,
        };
        var activeOutOfRange = new Contract
        {
            Code = "CNT-2026-00003",
            Client = client,
            Lot = lot,
            Origin = ContractOrigin.Legacy,
            Status = ContractStatus.Active,
            TotalPrice = 40000,
            DownPaymentAmount = 4000,
            AmountFinanced = 36000,
            MonthsFinanced = 12,
            Currency = Currency.SOLES,
            ExchangeRate = 1,
            StartDate = inRangeStart.AddMonths(-2),
        };

        context.Clients.Add(client);
        context.Projects.Add(project);
        context.Blocks.Add(block);
        context.Lots.AddRange(lot, otherLot);
        context.Contracts.AddRange(activeInRange, pendingInRange, activeOutOfRange);
        context.Payments.Add(
            new Payment
            {
                Contract = activeInRange,
                Type = PaymentType.Installment,
                Sequence = 1,
                DueDate = inRangeStart.AddMonths(1),
                AmountDue = 3000,
                Paid = false,
            }
        );
        await context.SaveChangesAsync();

        return new CreditsSeed(activeInRange, inRangeStart);
    }

    private static async Task<IncomeSeed> SeedIncomeAsync(DatabaseContext context)
    {
        var paymentDay = new DateTime(2026, 4, 10, 12, 0, 0, DateTimeKind.Utc);
        var advisor = new User
        {
            Id = Guid.NewGuid(),
            Name = "Asesor",
            UserName = "asesor-income",
            Email = "asesor-income@test.com",
        };
        var client = new Client
        {
            Name = "Luis Pago",
            Dni = "87654321",
            PhoneNumber = "988777666",
            Type = ClientType.Natural,
        };
        var project = new Project
        {
            Name = "Proyecto Sur",
            Location = "Lima",
            Currency = "PEN",
        };
        var block = new Block { Name = "B", Project = project };
        var lot = new Lot
        {
            LotNumber = "5",
            Area = 80,
            Price = 50000,
            Status = LotStatus.Reserved,
            Block = block,
        };
        var lead = new Lead
        {
            Code = "LEAD-2026-01000",
            Client = client,
            AssignedTo = advisor,
            CaptureSource = LeadCaptureSource.Company,
        };
        var quotation = new Quotation
        {
            Code = "COT-2026-01000",
            Lead = lead,
            Lot = lot,
            Advisor = advisor,
            AdvisorId = advisor.Id,
            TotalPrice = 50000,
            Discount = 0,
            FinalPrice = 50000,
            DownPayment = 10,
            AmountFinanced = 45000,
            MonthsFinanced = 12,
            AreaAtQuotation = 80,
            PricePerM2AtQuotation = 625,
            Currency = "PEN",
            ExchangeRate = 1,
            QuotationDate = "2026-04-01",
            ValidUntil = paymentDay.AddDays(30),
        };
        var reservation = new Reservation
        {
            Client = client,
            Quotation = quotation,
            ReservationDate = DateOnly.FromDateTime(paymentDay),
            AmountPaid = 500,
            TotalAmountRequired = 5000,
            RemainingAmount = 4500,
            ExchangeRate = 1,
            ExpiresAt = paymentDay.AddDays(4),
            Status = ReservationStatus.ISSUED,
            PaymentMethod = PaymentMethod.CASH,
        };
        var contract = new Contract
        {
            Code = "CNT-2026-01000",
            Client = client,
            Lot = lot,
            Origin = ContractOrigin.Direct,
            Status = ContractStatus.Active,
            TotalPrice = 50000,
            DownPaymentAmount = 5000,
            AmountFinanced = 45000,
            MonthsFinanced = 12,
            Currency = Currency.SOLES,
            ExchangeRate = 1,
            StartDate = paymentDay,
        };

        context.Users.Add(advisor);
        context.Clients.Add(client);
        context.Projects.Add(project);
        context.Blocks.Add(block);
        context.Lots.Add(lot);
        context.Leads.Add(lead);
        context.Quotations.Add(quotation);
        context.Reservations.Add(reservation);
        context.Contracts.Add(contract);
        context.PaymentTransactions.AddRange(
            new PaymentTransaction
            {
                PaymentDate = new DateTimeOffset(paymentDay),
                AmountPaid = 1000,
                Contract = contract,
                PaymentMethod = PaymentMethod.BANK_TRANSFER,
                ReferenceNumber = "OP-100",
            },
            new PaymentTransaction
            {
                PaymentDate = new DateTimeOffset(paymentDay),
                AmountPaid = 500,
                Reservation = reservation,
                PaymentMethod = PaymentMethod.CASH,
                ReferenceNumber = "SEP-1",
            },
            new PaymentTransaction
            {
                PaymentDate = new DateTimeOffset(paymentDay.AddDays(-10)),
                AmountPaid = 999,
                Contract = contract,
                PaymentMethod = PaymentMethod.CASH,
            }
        );
        await context.SaveChangesAsync();

        return new IncomeSeed(project, paymentDay);
    }

    private sealed record CreditsSeed(Contract ActiveInRange, DateTime InRangeStart);

    private sealed record IncomeSeed(Project Project, DateTime PaymentDay);

    private sealed class FakeExcelExportService : IExcelExportService
    {
        public List<string>? LastHeaders { get; private set; }
        public List<List<object>>? LastData { get; private set; }

        public byte[] GenerateExcel(string title, List<string> headers, List<List<object>> data)
        {
            LastHeaders = headers;
            LastData = data;
            return [1, 2, 3];
        }

        public byte[] GenerateExcel(
            string title,
            List<string> headers,
            List<List<object>> data,
            bool expandComplexDataHorizontally
        ) => GenerateExcel(title, headers, data);

        public byte[] GenerateExcel(
            string title,
            List<string> headers,
            List<List<object>> data,
            bool expandComplexDataHorizontally,
            List<int> complexDataColumnIndexes
        ) => GenerateExcel(title, headers, data);
    }
}
