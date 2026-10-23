using GestionHogar.Model;
using GestionHogar.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionHogar.Tests.Services;

public class ContractCreateFromReservationTests
{
    [Fact]
    public async Task CreateFromPaidReservation_GeneratesInstallmentsWhenMissing()
    {
        var context = CreateContext();
        var graph = await SeedAsync(context, withInstallments: false);
        graph.Reservation.Status = ReservationStatus.CANCELED;
        await context.SaveChangesAsync();

        var service = new ContractService(context, new PaymentService(context));
        var dto = await service.CreateFromPaidReservationAsync(graph.Reservation.Id);

        Assert.Equal(ContractStatus.PendingValidation, dto.Status);
        Assert.Equal(2, dto.MonthsFinanced);
        var installments = await context.Payments.CountAsync(p =>
            p.ContractId == dto.Id && p.Type == PaymentType.Installment
        );
        Assert.Equal(2, installments);
        Assert.Equal(
            1,
            await context.Payments.CountAsync(p =>
                p.ContractId == dto.Id && p.Type == PaymentType.DownPayment
            )
        );
    }

    [Fact]
    public async Task CreateFromPaidReservation_RejectsIssuedReservation()
    {
        var context = CreateContext();
        var graph = await SeedAsync(context, withInstallments: false);
        var service = new ContractService(context, new PaymentService(context));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateFromPaidReservationAsync(graph.Reservation.Id)
        );
    }

    [Fact]
    public async Task CreateFromPaidReservation_ReactivatesAfterLeavingPaid()
    {
        var context = CreateContext();
        var graph = await SeedAsync(context, withInstallments: false);
        graph.Reservation.Status = ReservationStatus.CANCELED;
        await context.SaveChangesAsync();

        var service = new ContractService(context, new PaymentService(context));
        var first = await service.CreateFromPaidReservationAsync(graph.Reservation.Id);

        await service.RescindIfLeavingPaidReservationAsync(graph.Reservation.Id);
        await context.SaveChangesAsync();
        Assert.False(await context.Contracts.AnyAsync(c => c.Id == first.Id && c.IsActive));

        graph.Reservation.Status = ReservationStatus.CANCELED;
        graph.Reservation.ContractValidationStatus = ContractValidationStatus.PendingValidation;
        await context.SaveChangesAsync();

        var second = await service.CreateFromPaidReservationAsync(graph.Reservation.Id);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(ContractStatus.PendingValidation, second.Status);
        Assert.Equal(1, await context.Contracts.CountAsync());
        Assert.True(await context.Contracts.AnyAsync(c => c.Id == first.Id && c.IsActive));
        Assert.Equal(
            2,
            await context.Payments.CountAsync(p =>
                p.ContractId == first.Id && p.Type == PaymentType.Installment && p.IsActive
            )
        );
    }

    [Fact]
    public async Task Rescind_BlocksActiveContractWithReceipts()
    {
        var context = CreateContext();
        var graph = await SeedAsync(context, withInstallments: true);
        graph.Reservation.Status = ReservationStatus.CANCELED;
        graph.Reservation.ContractValidationStatus = ContractValidationStatus.Validated;
        await context.SaveChangesAsync();

        var service = new ContractService(context, new PaymentService(context));
        var dto = await service.CreateFromPaidReservationAsync(graph.Reservation.Id);
        var contract = await context.Contracts.SingleAsync(c => c.Id == dto.Id);
        contract.Status = ContractStatus.Active;
        context.PaymentTransactions.Add(
            new PaymentTransaction
            {
                ContractId = contract.Id,
                ReservationId = graph.Reservation.Id,
                AmountPaid = 100,
                PaymentDate = DateTime.UtcNow,
                PaymentMethod = PaymentMethod.CASH,
            }
        );
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RescindIfLeavingPaidReservationAsync(graph.Reservation.Id)
        );
        Assert.Contains("recibos", ex.Message);
        Assert.True(await context.Contracts.AnyAsync(c => c.Id == contract.Id && c.IsActive));
    }

    private static DatabaseContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DatabaseContext(options);
    }

    private static async Task<(Reservation Reservation, Quotation Quotation)> SeedAsync(
        DatabaseContext context,
        bool withInstallments
    )
    {
        var advisor = new User
        {
            Id = Guid.NewGuid(),
            Name = "Asesor",
            UserName = "asesor2",
            Email = "asesor2@test.com",
        };
        var client = new Client
        {
            Name = "Luis Diaz",
            PhoneNumber = "999000111",
            Type = ClientType.Natural,
        };
        var project = new Project
        {
            Name = "P2",
            Location = "Cusco",
            Currency = "PEN",
        };
        var block = new Block { Name = "B", Project = project };
        var lot = new Lot
        {
            LotNumber = "3",
            Area = 80,
            Price = 40000,
            Status = LotStatus.Quoted,
            Block = block,
        };
        var lead = new Lead
        {
            Code = "LEAD-2026-00002",
            Client = client,
            AssignedTo = advisor,
            CaptureSource = LeadCaptureSource.Company,
        };
        var quotation = new Quotation
        {
            Code = "COT-2026-00002",
            Lead = lead,
            Lot = lot,
            Advisor = advisor,
            AdvisorId = advisor.Id,
            TotalPrice = 40000,
            Discount = 0,
            FinalPrice = 40000,
            DownPayment = 10,
            AmountFinanced = 36000,
            MonthsFinanced = 2,
            AreaAtQuotation = 80,
            PricePerM2AtQuotation = 500,
            Currency = "PEN",
            ExchangeRate = 1,
            QuotationDate = "2026-02-01",
            ValidUntil = DateTime.UtcNow.AddDays(30),
        };
        var reservation = new Reservation
        {
            Client = client,
            Quotation = quotation,
            ReservationDate = DateOnly.FromDateTime(DateTime.UtcNow),
            AmountPaid = 4000,
            TotalAmountRequired = 4000,
            RemainingAmount = 0,
            ExchangeRate = 1,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            Status = ReservationStatus.ISSUED,
            PaymentMethod = PaymentMethod.CASH,
            ContractValidationStatus = ContractValidationStatus.PendingValidation,
        };

        context.Users.Add(advisor);
        context.Clients.Add(client);
        context.Projects.Add(project);
        context.Blocks.Add(block);
        context.Lots.Add(lot);
        context.Leads.Add(lead);
        context.Quotations.Add(quotation);
        context.Reservations.Add(reservation);

        if (withInstallments)
        {
            context.Payments.Add(
                new Payment
                {
                    Reservation = reservation,
                    DueDate = DateTime.UtcNow.AddMonths(1),
                    AmountDue = 18000,
                    Type = PaymentType.Installment,
                    Sequence = 1,
                }
            );
        }

        await context.SaveChangesAsync();
        return (reservation, quotation);
    }
}
