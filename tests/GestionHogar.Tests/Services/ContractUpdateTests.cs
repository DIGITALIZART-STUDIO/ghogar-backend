using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;
using GestionHogar.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionHogar.Tests.Services;

public class ContractUpdateTests
{
    [Fact]
    public async Task Update_RejectsCommercialTermChangesWithoutScheduleEndpoint()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.UpdateAsync(
                    created.Id,
                    new UpdateContractDto
                    {
                        TotalPrice = 40000,
                        DownPaymentAmount = 4000,
                        MonthsFinanced = 4,
                        ExchangeRate = 1,
                    }
                )
        );
        Assert.Contains("recalcula y guarda el cronograma", ex.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            2,
            await context.Payments.CountAsync(p =>
                p.ContractId == created.Id && p.Type == PaymentType.Installment && p.IsActive
            )
        );
    }

    [Fact]
    public async Task Update_RejectsMonthsBelowPaidCount()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        foreach (
            var payment in context.Payments.Where(p =>
                p.ContractId == created.Id && p.Type == PaymentType.Installment
            )
        )
        {
            payment.Paid = true;
        }
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.UpdateAsync(
                    created.Id,
                    new UpdateContractDto
                    {
                        TotalPrice = 40000,
                        DownPaymentAmount = 4000,
                        MonthsFinanced = 1,
                        ExchangeRate = 1,
                    }
                )
        );
        Assert.Contains("cuotas ya pagadas", ex.Message);
    }

    [Fact]
    public async Task Update_RejectsDownPaymentChangeWithoutScheduleEndpoint()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.UpdateAsync(
                    created.Id,
                    new UpdateContractDto
                    {
                        TotalPrice = 50000,
                        DownPaymentAmount = 10000,
                        MonthsFinanced = 2,
                        ExchangeRate = 1,
                    }
                )
        );
        Assert.Contains("recalcula y guarda el cronograma", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_RejectsCompletedContract()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        var contract = await context.Contracts.SingleAsync(c => c.Id == created.Id);
        contract.Status = ContractStatus.Completed;
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.UpdateAsync(
                    created.Id,
                    new UpdateContractDto
                    {
                        TotalPrice = 41000,
                        DownPaymentAmount = 4000,
                        MonthsFinanced = 2,
                        ExchangeRate = 1,
                    }
                )
        );
    }

    [Fact]
    public async Task Update_CoOwnersWithoutTouchingSchedule()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        var before = await context.Payments.CountAsync(p => p.ContractId == created.Id && p.IsActive);

        var updated = await service.UpdateAsync(
            created.Id,
            new UpdateContractDto
            {
                TotalPrice = created.TotalPrice,
                DownPaymentAmount = created.DownPaymentAmount,
                MonthsFinanced = created.MonthsFinanced,
                ExchangeRate = 3.75m,
                CoOwners = """[{"name":"Luis Perez","dni":"12345678","address":"Arequipa"}]""",
            }
        );

        Assert.Equal(3.75m, updated.ExchangeRate);
        Assert.Contains("Luis Perez", updated.CoOwners);
        Assert.Equal(
            before,
            await context.Payments.CountAsync(p => p.ContractId == created.Id && p.IsActive)
        );
    }

    [Fact]
    public async Task Rescind_AllowsReceipts_FreesLot_KeepsHistory()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));

        context.PaymentTransactions.Add(
            new PaymentTransaction
            {
                ContractId = created.Id,
                AmountPaid = 500,
                PaymentDate = DateTime.UtcNow,
                PaymentMethod = PaymentMethod.CASH,
            }
        );
        await context.SaveChangesAsync();

        var rescinded = await service.RescindAsync(created.Id);

        Assert.Equal(ContractStatus.Rescinded, rescinded.Status);
        Assert.True(await context.Contracts.AnyAsync(c => c.Id == created.Id && c.IsActive));
        var lot = await context.Lots.FindAsync(graph.Lot.Id);
        Assert.Equal(LotStatus.Available, lot!.Status);
        Assert.Equal(
            1,
            await context.PaymentTransactions.CountAsync(t => t.ContractId == created.Id && t.IsActive)
        );
        Assert.Equal(
            3,
            await context.Payments.CountAsync(p => p.ContractId == created.Id && p.IsActive)
        );
    }

    [Fact]
    public async Task Rescind_RejectsAlreadyClosedContract()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        var contract = await context.Contracts.SingleAsync(c => c.Id == created.Id);
        contract.Status = ContractStatus.Completed;
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RescindAsync(created.Id)
        );
        Assert.Contains("no se puede anular", ex.Message);
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
