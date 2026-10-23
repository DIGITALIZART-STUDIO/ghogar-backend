using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;
using GestionHogar.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionHogar.Tests.Services;

public class ContractDirectLegacyAndToggleTests
{
    [Fact]
    public async Task Toggle_PendingValidationBecomesActive_AndBack()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));

        var created = await service.CreateAsync(
            DirectDto(graph.Client.Id, graph.Lot.Id)
        );
        Assert.Equal(ContractStatus.PendingValidation, created.Status);

        var activated = await service.ToggleValidationStatusAsync(created.Id);
        Assert.Equal(ContractStatus.Active, activated.Status);
        Assert.NotNull(activated.SignedAt);

        var pending = await service.ToggleValidationStatusAsync(created.Id);
        Assert.Equal(ContractStatus.PendingValidation, pending.Status);
        Assert.Null(pending.SignedAt);
    }

    [Fact]
    public async Task CreateDirect_GeneratesDownPaymentAndInstallments_ReservesLot()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));

        var dto = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));

        Assert.Equal(ContractOrigin.Direct, dto.Origin);
        Assert.Equal(ContractStatus.PendingValidation, dto.Status);
        Assert.StartsWith("CNT-", dto.Code);
        Assert.Equal(40000, dto.TotalPrice);
        Assert.Equal(36000, dto.AmountFinanced);
        Assert.Equal(0, dto.AmountPaid);
        Assert.Equal(4000, dto.NextPaymentAmount);
        Assert.Equal(40000, dto.RemainingAmount);
        Assert.Null(dto.ReservationId);

        var payments = await context.Payments.Where(p => p.ContractId == dto.Id).ToListAsync();
        Assert.Equal(3, payments.Count);
        Assert.Single(payments, p => p.Type == PaymentType.DownPayment && p.Sequence == 0 && !p.Paid);
        Assert.Equal(2, payments.Count(p => p.Type == PaymentType.Installment && !p.Paid));

        var lot = await context.Lots.FindAsync(graph.Lot.Id);
        Assert.Equal(LotStatus.Reserved, lot!.Status);
    }

    [Fact]
    public async Task CreateLegacy_MarksPaidInstallments_AndIsActive()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var dto = DirectDto(graph.Client.Id, graph.Lot.Id);
        dto.Origin = ContractOrigin.Legacy;
        dto.StartDate = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        dto.PaidInstallments = 6;
        dto.MonthsFinanced = 24;

        var created = await service.CreateAsync(dto);

        Assert.Equal(ContractStatus.Active, created.Status);
        Assert.Equal(ContractOrigin.Legacy, created.Origin);
        Assert.Equal(24, created.MonthsFinanced);
        Assert.Equal(13000, created.AmountPaid);
        Assert.Equal(1500, created.NextPaymentAmount);
        Assert.Equal(27000, created.RemainingAmount);

        var installments = await context
            .Payments.Where(p => p.ContractId == created.Id && p.Type == PaymentType.Installment)
            .OrderBy(p => p.Sequence)
            .ToListAsync();
        Assert.Equal(24, installments.Count);
        Assert.Equal(6, installments.Count(p => p.Paid));
        Assert.Equal(18, installments.Count(p => !p.Paid));
        Assert.True(
            await context.Payments.AnyAsync(p =>
                p.ContractId == created.Id && p.Type == PaymentType.DownPayment && p.Paid
            )
        );
    }

    [Fact]
    public async Task CreateDirect_RejectsLotWithActiveContract()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id))
        );
        Assert.Contains("lote ya tiene un contrato activo", ex.Message);
    }

    [Fact]
    public async Task FinancialSummary_SumsClientContracts()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var secondLot = new Lot
        {
            LotNumber = "9",
            Area = 90,
            Price = 50000,
            Status = LotStatus.Available,
            Block = graph.Lot.Block,
        };
        context.Lots.Add(secondLot);
        await context.SaveChangesAsync();

        var service = new ContractService(context, new PaymentService(context));
        await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        var second = DirectDto(graph.Client.Id, secondLot.Id);
        second.TotalPrice = 50000;
        second.DownPaymentAmount = 5000;
        await service.CreateAsync(second);

        var contracts = (await service.GetByClientIdAsync(graph.Client.Id)).ToList();
        Assert.Equal(2, contracts.Count);

        var summary = await service.GetFinancialSummaryByClientIdAsync(graph.Client.Id);
        Assert.Equal(2, summary.ActiveContracts);
        var soles = Assert.Single(summary.Balances);
        Assert.Equal(Currency.SOLES, soles.Currency);
        Assert.Equal(90000, soles.RemainingAmount);
        Assert.Equal(0, soles.TotalPaid);
    }

    [Fact]
    public async Task CreateDirect_RejectsReservedLot()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        graph.Lot.Status = LotStatus.Reserved;
        await context.SaveChangesAsync();
        var service = new ContractService(context, new PaymentService(context));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id))
        );
        Assert.Contains("no está disponible", ex.Message);
    }

    [Fact]
    public async Task CreateLegacy_RequiresStartDate()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var dto = DirectDto(graph.Client.Id, graph.Lot.Id);
        dto.Origin = ContractOrigin.Legacy;
        dto.StartDate = null;

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto));
        Assert.Contains("fecha de inicio", ex.Message);
    }

    [Fact]
    public async Task CreateDirect_RoundsLastInstallmentToFinancedTotal()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var dto = DirectDto(graph.Client.Id, graph.Lot.Id);
        dto.TotalPrice = 40000;
        dto.DownPaymentAmount = 4000;
        dto.MonthsFinanced = 7;

        var created = await service.CreateAsync(dto);
        var installments = await context
            .Payments.Where(p => p.ContractId == created.Id && p.Type == PaymentType.Installment)
            .OrderBy(p => p.Sequence)
            .ToListAsync();

        Assert.Equal(7, installments.Count);
        Assert.Equal(36000, installments.Sum(p => p.AmountDue));
        Assert.All(installments, p => Assert.Equal(p.AmountDue, Math.Round(p.AmountDue, 2)));
    }

    [Fact]
    public async Task CreateAfterRescind_AllowsNewContractOnSameLot()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var first = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        await service.RescindAsync(first.Id);

        var second = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(ContractStatus.PendingValidation, second.Status);
        Assert.Equal(LotStatus.Reserved, (await context.Lots.FindAsync(graph.Lot.Id))!.Status);
    }

    [Fact]
    public async Task FinancialSummary_ExcludesRescindedRemaining()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        await service.RescindAsync(created.Id);

        var summary = await service.GetFinancialSummaryByClientIdAsync(graph.Client.Id);

        Assert.Equal(0, summary.ActiveContracts);
        Assert.Empty(summary.Balances);
    }

    [Fact]
    public async Task Toggle_RejectsCompletedContract()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        var contract = await context.Contracts.SingleAsync(c => c.Id == created.Id);
        contract.Status = ContractStatus.Completed;
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ToggleValidationStatusAsync(created.Id)
        );
        Assert.Contains("completado o rescindido", ex.Message);
    }

    [Fact]
    public async Task GetPaginated_FiltersByStatusAndSearch()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var secondLot = new Lot
        {
            LotNumber = "22",
            Area = 90,
            Price = 50000,
            Status = LotStatus.Available,
            Block = graph.Lot.Block,
        };
        context.Lots.Add(secondLot);
        await context.SaveChangesAsync();

        var service = new ContractService(context, new PaymentService(context));
        var first = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        var secondDto = DirectDto(graph.Client.Id, secondLot.Id);
        secondDto.TotalPrice = 50000;
        secondDto.DownPaymentAmount = 5000;
        await service.CreateAsync(secondDto);
        await service.RescindAsync(first.Id);

        var pagination = new PaginationService();
        var pending = await service.GetPaginatedAsync(
            1,
            10,
            pagination,
            status: [ContractStatus.PendingValidation]
        );
        var rescinded = await service.GetPaginatedAsync(
            1,
            10,
            pagination,
            status: [ContractStatus.Rescinded]
        );
        var searched = await service.GetPaginatedAsync(1, 10, pagination, search: "Ana Ruiz");

        Assert.Equal(1, pending.Meta.Total);
        Assert.Equal(1, rescinded.Meta.Total);
        Assert.Equal(2, searched.Meta.Total);
    }

    [Fact]
    public async Task Create_RejectsPipelineOrigin()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var dto = DirectDto(graph.Client.Id, graph.Lot.Id);
        dto.Origin = ContractOrigin.Pipeline;

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto));
    }

    private static CreateContractDto DirectDto(Guid clientId, Guid lotId) =>
        new()
        {
            Origin = ContractOrigin.Direct,
            ClientId = clientId,
            LotId = lotId,
            TotalPrice = 40000,
            DownPaymentAmount = 4000,
            MonthsFinanced = 2,
            Currency = Currency.SOLES,
            ExchangeRate = 1,
        };

    private static DatabaseContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DatabaseContext(options);
    }

    private static async Task<(Client Client, Lot Lot)> SeedLotAndClientAsync(DatabaseContext context)
    {
        var client = new Client
        {
            Name = "Ana Ruiz",
            PhoneNumber = "999111222",
            Type = ClientType.Natural,
        };
        var project = new Project
        {
            Name = "P3",
            Location = "Arequipa",
            Currency = "PEN",
        };
        var block = new Block { Name = "C", Project = project };
        var lot = new Lot
        {
            LotNumber = "1",
            Area = 100,
            Price = 40000,
            Status = LotStatus.Available,
            Block = block,
        };
        context.Clients.Add(client);
        context.Projects.Add(project);
        context.Blocks.Add(block);
        context.Lots.Add(lot);
        await context.SaveChangesAsync();
        return (client, lot);
    }
}
