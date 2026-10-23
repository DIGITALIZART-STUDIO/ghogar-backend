using GestionHogar.Controllers.Dtos;
using GestionHogar.Dtos;
using GestionHogar.Model;
using GestionHogar.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionHogar.Tests.Services;

public class PaymentTransactionContractTests
{
    [Fact]
    public async Task Create_AssignsReceiptToContractInstallments()
    {
        var context = CreateContext();
        var graph = await SeedAsync(context);
        var service = new PaymentTransactionService(context, new NoopCloudflare());

        var created = await service.CreateAsync(
            new PaymentTransactionCreateDTO
            {
                PaymentDate = DateTime.UtcNow,
                AmountPaid = 18000,
                ContractId = graph.Contract.Id,
                PaymentMethod = PaymentMethod.CASH,
                StartFromLastCuota = false,
            }
        );

        Assert.Equal(graph.Contract.Id, created.ContractId);
        Assert.Equal(18000, created.AmountPaid);

        var paid = await context.Payments.CountAsync(p => p.ContractId == graph.Contract.Id && p.Paid);
        Assert.Equal(1, paid);

        var quota = await service.GetQuotaStatusByContractAsync(graph.Contract.Id);
        Assert.True(quota.TotalAmountRemaining < 36000);
        Assert.All(quota.PendingQuotas, q => Assert.Equal(graph.Contract.Id, q.ContractId));
    }

    [Fact]
    public async Task Create_StartFromLastFalse_PaysDownPaymentFirst()
    {
        var context = CreateContext();
        var graph = await SeedAsync(context);
        var service = new PaymentTransactionService(context, new NoopCloudflare());

        var created = await service.CreateAsync(
            new PaymentTransactionCreateDTO
            {
                PaymentDate = DateTime.UtcNow,
                AmountPaid = 4000,
                ContractId = graph.Contract.Id,
                PaymentMethod = PaymentMethod.CASH,
                StartFromLastCuota = false,
            }
        );

        Assert.Equal(graph.Contract.Id, created.ContractId);
        var downPayment = await context.Payments.SingleAsync(p =>
            p.ContractId == graph.Contract.Id && p.Type == PaymentType.DownPayment
        );
        Assert.True(downPayment.Paid);
        Assert.Equal(
            0,
            await context.Payments.CountAsync(p =>
                p.ContractId == graph.Contract.Id && p.Type == PaymentType.Installment && p.Paid
            )
        );

        var quota = await service.GetQuotaStatusByContractAsync(graph.Contract.Id);
        Assert.Equal(36000, quota.TotalAmountRemaining);
        Assert.Equal(2, quota.PendingQuotas.Count);
        Assert.DoesNotContain(quota.PendingQuotas, q => q.Id == downPayment.Id);
    }

    [Fact]
    public async Task Create_ResolvesContractFromReservationId()
    {
        var context = CreateContext();
        var graph = await SeedPipelineAsync(context);
        var service = new PaymentTransactionService(context, new NoopCloudflare());

        var created = await service.CreateAsync(
            new PaymentTransactionCreateDTO
            {
                PaymentDate = DateTime.UtcNow,
                AmountPaid = 4000,
                ReservationId = graph.Reservation.Id,
                PaymentMethod = PaymentMethod.CASH,
                StartFromLastCuota = false,
            }
        );

        Assert.Equal(graph.Contract.Id, created.ContractId);
        Assert.Equal(graph.Reservation.Id, created.ReservationId);
        Assert.True(
            await context.Payments.AnyAsync(p =>
                p.ContractId == graph.Contract.Id && p.Type == PaymentType.DownPayment && p.Paid
            )
        );
    }

    [Fact]
    public async Task GetByContractId_ReturnsReceipts()
    {
        var context = CreateContext();
        var graph = await SeedAsync(context);
        var service = new PaymentTransactionService(context, new NoopCloudflare());
        await service.CreateAsync(
            new PaymentTransactionCreateDTO
            {
                PaymentDate = DateTime.UtcNow,
                AmountPaid = 4000,
                ContractId = graph.Contract.Id,
                PaymentMethod = PaymentMethod.BANK_TRANSFER,
                StartFromLastCuota = false,
            }
        );

        var receipts = (await service.GetByContractIdAsync(graph.Contract.Id)).ToList();

        Assert.Single(receipts);
        Assert.Equal(4000, receipts[0].AmountPaid);
        Assert.Equal(PaymentMethod.BANK_TRANSFER, receipts[0].PaymentMethod);
    }

    [Fact]
    public async Task QuotaStatus_IncludesDownPaymentBeforeInstallments()
    {
        var context = CreateContext();
        var graph = await SeedAsync(context);
        var service = new PaymentTransactionService(context, new NoopCloudflare());

        var quota = await service.GetQuotaStatusByContractAsync(graph.Contract.Id);

        Assert.Equal(3, quota.PendingQuotas.Count);
        Assert.Equal(40000, quota.TotalAmountRemaining);
        Assert.Equal(
            PaymentType.DownPayment,
            context.Payments.Single(p => p.Id == quota.PendingQuotas[0].Id).Type
        );
        Assert.Equal(graph.Contract.Id, quota.PendingQuotas[0].ContractId);
    }

    private static DatabaseContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DatabaseContext(options);
    }

    private static async Task<(Contract Contract, Client Client)> SeedAsync(DatabaseContext context)
    {
        var client = new Client
        {
            Name = "Pago Test",
            PhoneNumber = "111222333",
            Type = ClientType.Natural,
        };
        var project = new Project
        {
            Name = "P4",
            Location = "Lima",
            Currency = "PEN",
        };
        var block = new Block { Name = "D", Project = project };
        var lot = new Lot
        {
            LotNumber = "8",
            Area = 70,
            Price = 40000,
            Status = LotStatus.Available,
            Block = block,
        };
        context.Clients.Add(client);
        context.Projects.Add(project);
        context.Blocks.Add(block);
        context.Lots.Add(lot);
        await context.SaveChangesAsync();

        var contractService = new ContractService(context, new PaymentService(context));
        var dto = await contractService.CreateAsync(
            new CreateContractDto
            {
                Origin = ContractOrigin.Direct,
                ClientId = client.Id,
                LotId = lot.Id,
                TotalPrice = 40000,
                DownPaymentAmount = 4000,
                MonthsFinanced = 2,
                Currency = Currency.SOLES,
                ExchangeRate = 1,
            }
        );
        var contract = await context.Contracts.SingleAsync(c => c.Id == dto.Id);
        return (contract, client);
    }

    private static async Task<(Contract Contract, Reservation Reservation)> SeedPipelineAsync(
        DatabaseContext context
    )
    {
        var advisor = new User
        {
            Id = Guid.NewGuid(),
            Name = "Asesor",
            UserName = "asesor-pt",
            Email = "asesor-pt@test.com",
        };
        var client = new Client
        {
            Name = "Pipeline Pago",
            PhoneNumber = "444555666",
            Type = ClientType.Natural,
        };
        var project = new Project
        {
            Name = "P5",
            Location = "Lima",
            Currency = "PEN",
        };
        var block = new Block { Name = "E", Project = project };
        var lot = new Lot
        {
            LotNumber = "4",
            Area = 70,
            Price = 40000,
            Status = LotStatus.Reserved,
            Block = block,
        };
        var lead = new Lead
        {
            Code = "LEAD-2026-00009",
            Client = client,
            AssignedTo = advisor,
            CaptureSource = LeadCaptureSource.Company,
        };
        var quotation = new Quotation
        {
            Code = "COT-2026-00009",
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
            AreaAtQuotation = 70,
            PricePerM2AtQuotation = 571.43m,
            Currency = "PEN",
            ExchangeRate = 1,
            QuotationDate = "2026-03-01",
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
            Status = ReservationStatus.CANCELED,
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
        await context.SaveChangesAsync();

        var contractService = new ContractService(context, new PaymentService(context));
        var dto = await contractService.CreateFromPaidReservationAsync(reservation.Id);
        var contract = await context.Contracts.SingleAsync(c => c.Id == dto.Id);
        return (contract, reservation);
    }

    private sealed class NoopCloudflare : ICloudflareService
    {
        public Task<string> UploadImageAsync(IFormFile file) => Task.FromResult("");

        public Task<string> UpdateImageAsync(IFormFile file, string existingFileName) => Task.FromResult("");

        public Task<string> UploadProjectImageAsync(IFormFile file, string projectName) =>
            Task.FromResult("");

        public Task<string> UpdateProjectImageAsync(
            IFormFile file,
            string projectName,
            string? existingImageUrl
        ) => Task.FromResult("");

        public Task<string> UploadPaymentReceiptImageAsync(IFormFile file, string transactionId) =>
            Task.FromResult("");

        public Task<string> UpdatePaymentReceiptImageAsync(
            IFormFile file,
            string transactionId,
            string? existingImageUrl
        ) => Task.FromResult("");

        public Task<bool> DeletePaymentTransactionFolderAsync(string transactionId) =>
            Task.FromResult(true);

        public Task<(bool Success, string Message, string? ErrorDetails)> TestConnectionAsync() =>
            Task.FromResult((true, "ok", (string?)null));
    }
}
