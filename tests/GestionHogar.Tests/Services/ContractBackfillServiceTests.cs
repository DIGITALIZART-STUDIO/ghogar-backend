using System.Text.Json;
using System.Text.Json.Serialization;
using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;
using GestionHogar.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionHogar.Tests.Services;

public class ContractBackfillServiceTests
{
    [Fact]
    public async Task Backfill_CreatesPipelineContractAndLinksInstallments()
    {
        var context = CreateContext();
        var graph = await SeedCanceledReservationAsync(context);
        var service = new ContractBackfillService(context);

        var result = await service.BackfillFromReservationsAsync();

        Assert.Equal(1, result.ContractsCreated);
        Assert.Equal(2, result.PaymentsLinked);
        Assert.Equal(1, result.DownPaymentsCreated);

        var contract = await context.Contracts.Include(c => c.Payments).SingleAsync();
        Assert.Equal(ContractOrigin.Pipeline, contract.Origin);
        Assert.Equal(ContractStatus.PendingValidation, contract.Status);
        Assert.Equal(graph.Reservation.Id, contract.ReservationId);
        Assert.Equal(graph.Quotation.Id, contract.QuotationId);
        Assert.StartsWith("CNT-", contract.Code);
        Assert.Equal(3, contract.Payments.Count(p => p.IsActive));
        Assert.Contains(
            contract.Payments,
            p => p.Type == PaymentType.DownPayment && p.Sequence == 0
        );
        Assert.Equal(
            2,
            contract.Payments.Count(p => p.Type == PaymentType.Installment)
        );
    }

    [Fact]
    public async Task Backfill_IsIdempotent()
    {
        var context = CreateContext();
        await SeedCanceledReservationAsync(context);
        var service = new ContractBackfillService(context);

        var first = await service.BackfillFromReservationsAsync();
        var second = await service.BackfillFromReservationsAsync();

        Assert.Equal(1, first.ContractsCreated);
        Assert.Equal(0, second.ContractsCreated);
        Assert.Equal(1, await context.Contracts.CountAsync());
        Assert.Equal(
            1,
            await context.Payments.CountAsync(p => p.Type == PaymentType.DownPayment)
        );
    }

    [Fact]
    public async Task Backfill_ConfirmedHistoryBecomesDownPaymentReceipt()
    {
        var context = CreateContext();
        var graph = await SeedCanceledReservationAsync(context, amountPaid: 5000m);
        var service = new ContractBackfillService(context);

        await service.BackfillFromReservationsAsync();

        var downPayment = await context.Payments.SingleAsync(p =>
            p.Type == PaymentType.DownPayment
        );
        Assert.True(downPayment.Paid);

        var allocations = await context
            .PaymentTransactionPayments.Where(ptp => ptp.PaymentId == downPayment.Id)
            .ToListAsync();
        Assert.Single(allocations);
        Assert.Equal(5000m, allocations[0].AmountPaid);

        var transaction = await context.PaymentTransactions.SingleAsync();
        Assert.Equal(graph.Reservation.Id, transaction.ReservationId);
        Assert.NotNull(transaction.ContractId);
    }

    [Fact]
    public async Task Backfill_SkipsIssuedReservations()
    {
        var context = CreateContext();
        await SeedCanceledReservationAsync(context, status: ReservationStatus.ISSUED);
        var service = new ContractBackfillService(context);

        var result = await service.BackfillFromReservationsAsync();

        Assert.Equal(0, result.ContractsCreated);
        Assert.Empty(context.Contracts);
    }

    [Fact]
    public async Task GetById_ReturnsBackfilledContract()
    {
        var context = CreateContext();
        await SeedCanceledReservationAsync(context);
        await new ContractBackfillService(context).BackfillFromReservationsAsync();
        var paymentService = new PaymentService(context);
        var contractService = new ContractService(context, paymentService);

        var created = await context.Contracts.SingleAsync();
        var dto = await contractService.GetByIdAsync(created.Id);

        Assert.NotNull(dto);
        Assert.Equal("Ana Perez", dto!.ClientName);
        Assert.Equal("P1", dto.ProjectName);
        Assert.True(dto.PaymentCount >= 3);

        var schedule = (await contractService.GetScheduleByIdAsync(created.Id)).ToList();
        Assert.Contains(schedule, p => p.Type == PaymentType.DownPayment);
        Assert.Equal(2, schedule.Count(p => p.Type == PaymentType.Installment));
    }

    private static DatabaseContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DatabaseContext(options);
    }

    private static async Task<SeedGraph> SeedCanceledReservationAsync(
        DatabaseContext context,
        ReservationStatus status = ReservationStatus.CANCELED,
        decimal amountPaid = 5000m
    )
    {
        var advisor = new User
        {
            Id = Guid.NewGuid(),
            Name = "Asesor",
            UserName = "asesor",
            Email = "asesor@test.com",
        };
        var client = new Client
        {
            Name = "Ana Perez",
            PhoneNumber = "999111222",
            Type = ClientType.Natural,
        };
        var project = new Project
        {
            Name = "P1",
            Location = "Lima",
            Currency = "PEN",
        };
        var block = new Block { Name = "A", Project = project };
        var lot = new Lot
        {
            LotNumber = "12",
            Area = 120,
            Price = 50000,
            Status = LotStatus.Reserved,
            Block = block,
        };
        var lead = new Lead
        {
            Code = "LEAD-2026-00001",
            Client = client,
            AssignedTo = advisor,
            CaptureSource = LeadCaptureSource.Company,
            Status = LeadStatus.InFollowUp,
        };
        var quotation = new Quotation
        {
            Code = "COT-2026-00001",
            Lead = lead,
            Lot = lot,
            Advisor = advisor,
            AdvisorId = advisor.Id,
            TotalPrice = 50000,
            Discount = 0,
            FinalPrice = 50000,
            DownPayment = 10,
            AmountFinanced = 45000,
            MonthsFinanced = 2,
            AreaAtQuotation = 120,
            PricePerM2AtQuotation = 416.66m,
            Currency = "PEN",
            ExchangeRate = 1,
            QuotationDate = "2026-01-15",
            ValidUntil = DateTime.UtcNow.AddDays(30),
        };

        var history = JsonSerializer.Serialize(
            new List<PaymentHistoryDto>
            {
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    Date = DateTime.UtcNow.AddDays(-2),
                    Amount = amountPaid,
                    Method = PaymentMethod.CASH,
                    Status = PaymentStatus.CONFIRMED,
                    Notes = "Enganche",
                },
            },
            new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } }
        );

        var reservation = new Reservation
        {
            Client = client,
            Quotation = quotation,
            ReservationDate = DateOnly.FromDateTime(DateTime.UtcNow),
            AmountPaid = amountPaid,
            TotalAmountRequired = 5000,
            RemainingAmount = Math.Max(0, 5000 - amountPaid),
            ExchangeRate = 1,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            Status = status,
            PaymentMethod = PaymentMethod.CASH,
            ContractValidationStatus = ContractValidationStatus.PendingValidation,
            PaymentHistory = history,
        };

        var p1 = new Payment
        {
            Reservation = reservation,
            DueDate = DateTime.UtcNow.AddMonths(1),
            AmountDue = 22500,
            Paid = false,
        };
        var p2 = new Payment
        {
            Reservation = reservation,
            DueDate = DateTime.UtcNow.AddMonths(2),
            AmountDue = 22500,
            Paid = false,
        };
        reservation.Payments.Add(p1);
        reservation.Payments.Add(p2);

        context.Users.Add(advisor);
        context.Clients.Add(client);
        context.Projects.Add(project);
        context.Blocks.Add(block);
        context.Lots.Add(lot);
        context.Leads.Add(lead);
        context.Quotations.Add(quotation);
        context.Reservations.Add(reservation);
        context.Payments.AddRange(p1, p2);
        await context.SaveChangesAsync();

        return new SeedGraph(reservation, quotation);
    }

    private sealed record SeedGraph(Reservation Reservation, Quotation Quotation);
}
