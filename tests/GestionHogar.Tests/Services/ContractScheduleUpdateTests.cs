using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;
using GestionHogar.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionHogar.Tests.Services;

public class ContractScheduleUpdateTests
{
    [Fact]
    public async Task Preview_WithMoreMonths_DoesNotPersist()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        var before = await context.Payments.CountAsync(p => p.ContractId == created.Id && p.IsActive);

        var preview = (
            await service.PreviewScheduleAsync(
                created.Id,
                new ContractSchedulePreviewDto
                {
                    TotalPrice = 40000,
                    DownPaymentAmount = 4000,
                    MonthsFinanced = 4,
                    StartDate = created.StartDate,
                }
            )
        ).ToList();

        Assert.Equal(5, preview.Count);
        Assert.Equal(4, preview.Count(p => p.Type == PaymentType.Installment));
        Assert.Equal(4000, preview.Single(p => p.Type == PaymentType.DownPayment).AmountDue);
        Assert.Equal(36000, preview.Where(p => p.Type == PaymentType.Installment).Sum(p => p.AmountDue));
        Assert.Equal(
            before,
            await context.Payments.CountAsync(p => p.ContractId == created.Id && p.IsActive)
        );
        var persisted = await context.Contracts.SingleAsync(c => c.Id == created.Id);
        Assert.Equal(40000, persisted.TotalPrice);
        Assert.Equal(2, persisted.MonthsFinanced);
    }

    [Fact]
    public async Task Preview_RejectsMonthsBelowPaidCount()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        await MarkFirstInstallmentPaidAsync(context, created.Id);
        await MarkInstallmentPaidAsync(context, created.Id, 2);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.PreviewScheduleAsync(
                    created.Id,
                    new ContractSchedulePreviewDto
                    {
                        TotalPrice = 40000,
                        DownPaymentAmount = 4000,
                        MonthsFinanced = 1,
                    }
                )
        );
        Assert.Contains("cuotas ya pagadas", ex.Message);
    }

    [Fact]
    public async Task UpdateSchedule_ChangesOnlyUnlockedDueDates()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        await MarkFirstInstallmentPaidAsync(context, created.Id);

        var installments = await ActiveInstallmentsAsync(context, created.Id);
        var originalAmounts = installments.Select(p => p.AmountDue).ToList();
        var lockedDate = installments[0].DueDate;
        var newDate = UtcDate(installments[1].DueDate.AddDays(5));

        await service.UpdateScheduleAsync(
            created.Id,
            ScheduleDto(
                created,
                ApplyItems(installments, 2, newDate)
            )
        );

        var updated = await ActiveInstallmentsAsync(context, created.Id);
        Assert.Equal(2, updated.Count);
        Assert.Equal(UtcDate(lockedDate), UtcDate(updated[0].DueDate));
        Assert.Equal(newDate, UtcDate(updated[1].DueDate));
        Assert.Equal(originalAmounts, updated.Select(p => p.AmountDue).ToList());
        Assert.True(updated[0].Paid);
        Assert.False(updated[1].Paid);
    }

    [Fact]
    public async Task UpdateSchedule_RejectsLockedDueDateChange()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        await MarkFirstInstallmentPaidAsync(context, created.Id);

        var installments = await ActiveInstallmentsAsync(context, created.Id);
        var items = ApplyItems(installments, 1, UtcDate(installments[0].DueDate.AddDays(3)));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateScheduleAsync(created.Id, ScheduleDto(created, items))
        );
        Assert.Contains("pagada o con recibos", ex.Message);
    }

    [Fact]
    public async Task UpdateSchedule_RejectsTermChangesWithoutRecalculate()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        var installments = await ActiveInstallmentsAsync(context, created.Id);

        var dto = ScheduleDto(created, ItemsFrom(installments));
        dto.MonthsFinanced = 4;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateScheduleAsync(created.Id, dto)
        );
        Assert.Contains("Recalcula las cuotas", ex.Message);
    }

    [Fact]
    public async Task UpdateSchedule_RecalculateAndSave_KeepsPaidInstallment()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        await MarkFirstInstallmentPaidAsync(context, created.Id);

        var preview = (
            await service.PreviewScheduleAsync(
                created.Id,
                new ContractSchedulePreviewDto
                {
                    TotalPrice = 40000,
                    DownPaymentAmount = 4000,
                    MonthsFinanced = 4,
                    StartDate = created.StartDate,
                }
            )
        ).ToList();

        var updated = await service.UpdateScheduleAsync(
            created.Id,
            new UpdateContractScheduleDto
            {
                TotalPrice = 40000,
                DownPaymentAmount = 4000,
                MonthsFinanced = 4,
                ExchangeRate = created.ExchangeRate,
                StartDate = created.StartDate,
                ApplyRecalculatedSchedule = true,
                Installments = preview
                    .Where(p => p.Type == PaymentType.Installment)
                    .Select(p => new UpdateContractScheduleItemDto
                    {
                        Sequence = p.Sequence,
                        DueDate = p.DueDate,
                    })
                    .ToList(),
            }
        );

        Assert.Equal(4, updated.MonthsFinanced);
        var installments = await ActiveInstallmentsAsync(context, created.Id);
        Assert.Equal(4, installments.Count);
        Assert.True(installments[0].Paid);
        Assert.Equal(18000, installments[0].AmountDue);
        Assert.Equal(3, installments.Count(p => !p.Paid));
        Assert.Equal(18000, installments.Where(p => !p.Paid).Sum(p => p.AmountDue));
        Assert.Equal(22000, updated.RemainingAmount);
    }

    [Fact]
    public async Task UpdateSchedule_RejectsOutOfOrderDates()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        var installments = await ActiveInstallmentsAsync(context, created.Id);
        var earlier = UtcDate(installments[0].DueDate.AddDays(-10));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.UpdateScheduleAsync(
                    created.Id,
                    ScheduleDto(created, ApplyItems(installments, 2, earlier))
                )
        );
        Assert.Contains("orden cronológico", ex.Message);
    }

    [Fact]
    public async Task UpdateSchedule_DoesNotTouchDownPayment()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        var downPayment = await context.Payments.SingleAsync(p =>
            p.ContractId == created.Id && p.Type == PaymentType.DownPayment && p.IsActive
        );
        var originalDue = downPayment.DueDate;
        var originalAmount = downPayment.AmountDue;
        var installments = await ActiveInstallmentsAsync(context, created.Id);
        var newDate = UtcDate(installments[1].DueDate.AddDays(2));

        await service.UpdateScheduleAsync(
            created.Id,
            ScheduleDto(created, ApplyItems(installments, 2, newDate))
        );

        var after = await context.Payments.SingleAsync(p =>
            p.ContractId == created.Id && p.Type == PaymentType.DownPayment && p.IsActive
        );
        Assert.Equal(originalAmount, after.AmountDue);
        Assert.Equal(UtcDate(originalDue), UtcDate(after.DueDate));
        Assert.Equal(downPayment.Id, after.Id);
    }

    [Fact]
    public async Task UpdateSchedule_RejectsCompletedContract()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        var contract = await context.Contracts.SingleAsync(c => c.Id == created.Id);
        contract.Status = ContractStatus.Completed;
        await context.SaveChangesAsync();
        var installments = await ActiveInstallmentsAsync(context, created.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateScheduleAsync(created.Id, ScheduleDto(created, ItemsFrom(installments)))
        );
    }

    [Fact]
    public async Task Preview_LastInstallmentAbsorbsRoundingRemainder()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));

        var preview = (
            await service.PreviewScheduleAsync(
                created.Id,
                new ContractSchedulePreviewDto
                {
                    TotalPrice = 40000,
                    DownPaymentAmount = 4000,
                    MonthsFinanced = 7,
                    StartDate = created.StartDate,
                }
            )
        ).ToList();

        var installments = preview.Where(p => p.Type == PaymentType.Installment).OrderBy(p => p.Sequence).ToList();
        Assert.Equal(7, installments.Count);
        Assert.Equal(36000, installments.Sum(p => p.AmountDue));
        Assert.Equal(5142.86m, installments[0].AmountDue);
        Assert.Equal(5142.84m, installments[^1].AmountDue);
    }

    [Fact]
    public async Task UpdateSchedule_RebuildsUnlockedDownPaymentAndDeactivatesOldRows()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        var originalDownPaymentId = (
            await context.Payments.SingleAsync(p =>
                p.ContractId == created.Id && p.Type == PaymentType.DownPayment && p.IsActive
            )
        ).Id;

        var preview = (
            await service.PreviewScheduleAsync(
                created.Id,
                new ContractSchedulePreviewDto
                {
                    TotalPrice = 50000,
                    DownPaymentAmount = 10000,
                    MonthsFinanced = 2,
                    StartDate = created.StartDate,
                }
            )
        ).ToList();

        var updated = await service.UpdateScheduleAsync(
            created.Id,
            new UpdateContractScheduleDto
            {
                TotalPrice = 50000,
                DownPaymentAmount = 10000,
                MonthsFinanced = 2,
                ExchangeRate = created.ExchangeRate,
                StartDate = created.StartDate,
                ApplyRecalculatedSchedule = true,
                Installments = preview
                    .Where(p => p.Type == PaymentType.Installment)
                    .Select(p => new UpdateContractScheduleItemDto
                    {
                        Sequence = p.Sequence,
                        DueDate = p.DueDate,
                    })
                    .ToList(),
            }
        );

        Assert.Equal(10000, updated.DownPaymentAmount);
        Assert.Equal(40000, updated.AmountFinanced);
        var downPayment = await context.Payments.SingleAsync(p =>
            p.ContractId == created.Id && p.Type == PaymentType.DownPayment && p.IsActive
        );
        Assert.Equal(10000, downPayment.AmountDue);
        Assert.NotEqual(originalDownPaymentId, downPayment.Id);
        Assert.False(
            await context.Payments.AnyAsync(p => p.Id == originalDownPaymentId && p.IsActive)
        );
        Assert.Equal(
            40000,
            await context
                .Payments.Where(p =>
                    p.ContractId == created.Id && p.Type == PaymentType.Installment && p.IsActive
                )
                .SumAsync(p => p.AmountDue)
        );
    }

    [Fact]
    public async Task UpdateSchedule_RejectsLockedDownPaymentAmountChange()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        var downPayment = await context.Payments.SingleAsync(p =>
            p.ContractId == created.Id && p.Type == PaymentType.DownPayment && p.IsActive
        );
        downPayment.Paid = true;
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.PreviewScheduleAsync(
                    created.Id,
                    new ContractSchedulePreviewDto
                    {
                        TotalPrice = 50000,
                        DownPaymentAmount = 10000,
                        MonthsFinanced = 2,
                    }
                )
        );
        Assert.Contains("inicial", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Preview_KeepsLockedDownPaymentIdentity()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        var downPayment = await context.Payments.SingleAsync(p =>
            p.ContractId == created.Id && p.Type == PaymentType.DownPayment && p.IsActive
        );
        downPayment.Paid = true;
        await context.SaveChangesAsync();

        var preview = (
            await service.PreviewScheduleAsync(
                created.Id,
                new ContractSchedulePreviewDto
                {
                    TotalPrice = 40000,
                    DownPaymentAmount = 4000,
                    MonthsFinanced = 3,
                    StartDate = created.StartDate,
                }
            )
        ).ToList();

        var previewDownPayment = preview.Single(p => p.Type == PaymentType.DownPayment);
        Assert.Equal(downPayment.Id, previewDownPayment.Id);
        Assert.Equal(4000, previewDownPayment.AmountDue);
        Assert.True(previewDownPayment.Paid);
        Assert.Equal(3, preview.Count(p => p.Type == PaymentType.Installment));
    }

    [Fact]
    public async Task UpdateSchedule_ReceiptLocksDueDateWithoutPaidFlag()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        var installments = await ActiveInstallmentsAsync(context, created.Id);
        await AddReceiptAsync(context, created.Id, installments[0].Id, 500);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.UpdateScheduleAsync(
                    created.Id,
                    ScheduleDto(
                        created,
                        ApplyItems(installments, 1, UtcDate(installments[0].DueDate.AddDays(3)))
                    )
                )
        );
        Assert.Contains("pagada o con recibos", ex.Message);
    }

    [Fact]
    public async Task UpdateSchedule_RecalculateKeepsReceiptLockedInstallment()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        var first = (await ActiveInstallmentsAsync(context, created.Id))[0];
        var originalId = first.Id;
        await AddReceiptAsync(context, created.Id, originalId, 500);

        var preview = (
            await service.PreviewScheduleAsync(
                created.Id,
                new ContractSchedulePreviewDto
                {
                    TotalPrice = 40000,
                    DownPaymentAmount = 4000,
                    MonthsFinanced = 4,
                    StartDate = created.StartDate,
                }
            )
        ).ToList();

        await service.UpdateScheduleAsync(
            created.Id,
            new UpdateContractScheduleDto
            {
                TotalPrice = 40000,
                DownPaymentAmount = 4000,
                MonthsFinanced = 4,
                ExchangeRate = created.ExchangeRate,
                StartDate = created.StartDate,
                ApplyRecalculatedSchedule = true,
                Installments = preview
                    .Where(p => p.Type == PaymentType.Installment)
                    .Select(p => new UpdateContractScheduleItemDto
                    {
                        Sequence = p.Sequence,
                        DueDate = p.DueDate,
                    })
                    .ToList(),
            }
        );

        var updated = await ActiveInstallmentsAsync(context, created.Id);
        Assert.Equal(4, updated.Count);
        Assert.Equal(originalId, updated[0].Id);
        Assert.False(updated[0].Paid);
        Assert.Equal(18000, updated[0].AmountDue);
        Assert.Equal(18000, updated.Where(p => p.Id != originalId).Sum(p => p.AmountDue));
    }

    [Fact]
    public async Task UpdateSchedule_RejectsMismatchedInstallmentCount()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        var installments = await ActiveInstallmentsAsync(context, created.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.UpdateScheduleAsync(
                    created.Id,
                    ScheduleDto(created, ItemsFrom(installments.Take(1)))
                )
        );
        Assert.Contains("no coincide", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateSchedule_RejectsDuplicateSequences()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        var installments = await ActiveInstallmentsAsync(context, created.Id);
        var items = ItemsFrom(installments);
        items[1].Sequence = items[0].Sequence;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateScheduleAsync(created.Id, ScheduleDto(created, items))
        );
        Assert.Contains("duplicadas", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateSchedule_RejectsWhenFinancedLessThanPaidInstallments()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        await MarkFirstInstallmentPaidAsync(context, created.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.PreviewScheduleAsync(
                    created.Id,
                    new ContractSchedulePreviewDto
                    {
                        TotalPrice = 20000,
                        DownPaymentAmount = 4000,
                        MonthsFinanced = 2,
                    }
                )
        );
        Assert.Contains("financiamiento menor", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateSchedule_UpdatesExchangeRateAndCoOwnersWithoutRebuild()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        var beforeIds = (await ActiveInstallmentsAsync(context, created.Id)).Select(p => p.Id).ToList();

        var updated = await service.UpdateScheduleAsync(
            created.Id,
            new UpdateContractScheduleDto
            {
                TotalPrice = created.TotalPrice,
                DownPaymentAmount = created.DownPaymentAmount,
                MonthsFinanced = created.MonthsFinanced,
                ExchangeRate = 3.8m,
                StartDate = created.StartDate,
                CoOwners = """[{"name":"Maria Lopez","dni":"87654321","address":"Cusco"}]""",
                ApplyRecalculatedSchedule = false,
                Installments = ItemsFrom(await ActiveInstallmentsAsync(context, created.Id)),
            }
        );

        Assert.Equal(3.8m, updated.ExchangeRate);
        Assert.Contains("Maria Lopez", updated.CoOwners);
        Assert.Equal(beforeIds, (await ActiveInstallmentsAsync(context, created.Id)).Select(p => p.Id).ToList());
    }

    [Fact]
    public async Task UpdateSchedule_RejectsStartDateChangeWithoutRecalculate()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        var installments = await ActiveInstallmentsAsync(context, created.Id);
        var dto = ScheduleDto(created, ItemsFrom(installments));
        dto.StartDate = UtcDate(created.StartDate.AddMonths(1));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateScheduleAsync(created.Id, dto)
        );
        Assert.Contains("Recalcula las cuotas", ex.Message);
    }

    [Fact]
    public async Task UpdateSchedule_AppliesCustomDueDatesAfterRecalculate()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));

        var preview = (
            await service.PreviewScheduleAsync(
                created.Id,
                new ContractSchedulePreviewDto
                {
                    TotalPrice = 40000,
                    DownPaymentAmount = 4000,
                    MonthsFinanced = 3,
                    StartDate = created.StartDate,
                }
            )
        ).ToList();
        var customDate = UtcDate(preview.Last(p => p.Type == PaymentType.Installment).DueDate.AddDays(10));
        var items = preview
            .Where(p => p.Type == PaymentType.Installment)
            .Select(p => new UpdateContractScheduleItemDto
            {
                Sequence = p.Sequence,
                DueDate = p.Sequence == 3 ? customDate : p.DueDate,
            })
            .ToList();

        await service.UpdateScheduleAsync(
            created.Id,
            new UpdateContractScheduleDto
            {
                TotalPrice = 40000,
                DownPaymentAmount = 4000,
                MonthsFinanced = 3,
                ExchangeRate = created.ExchangeRate,
                StartDate = created.StartDate,
                ApplyRecalculatedSchedule = true,
                Installments = items,
            }
        );

        var schedule = (await service.GetScheduleByIdAsync(created.Id)).ToList();
        Assert.Equal(customDate, UtcDate(schedule.Single(p => p.Sequence == 3).DueDate));
        Assert.Equal(3, schedule.Count(p => p.Type == PaymentType.Installment));
        Assert.Equal(36000, schedule.Where(p => p.Type == PaymentType.Installment).Sum(p => p.AmountDue));
    }

    [Fact]
    public async Task Preview_RejectsMissingContract()
    {
        var context = CreateContext();
        var service = new ContractService(context, new PaymentService(context));

        await Assert.ThrowsAsync<ArgumentException>(
            () =>
                service.PreviewScheduleAsync(
                    Guid.NewGuid(),
                    new ContractSchedulePreviewDto
                    {
                        TotalPrice = 40000,
                        DownPaymentAmount = 4000,
                        MonthsFinanced = 2,
                    }
                )
        );
    }

    [Fact]
    public async Task UpdateSchedule_PersistsCustomDueDateForPdfSource()
    {
        var context = CreateContext();
        var graph = await SeedLotAndClientAsync(context);
        var service = new ContractService(context, new PaymentService(context));
        var created = await service.CreateAsync(DirectDto(graph.Client.Id, graph.Lot.Id));
        var installments = await ActiveInstallmentsAsync(context, created.Id);
        var newDate = UtcDate(installments[1].DueDate.AddDays(7));

        await service.UpdateScheduleAsync(
            created.Id,
            ScheduleDto(created, ApplyItems(installments, 2, newDate))
        );

        var schedule = (await service.GetScheduleByIdAsync(created.Id)).ToList();
        var second = schedule.Single(p => p.Type == PaymentType.Installment && p.Sequence == 2);
        Assert.Equal(newDate, UtcDate(second.DueDate));
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

    private static UpdateContractScheduleDto ScheduleDto(
        ContractDto created,
        List<UpdateContractScheduleItemDto> items,
        bool applyRecalculated = false
    ) =>
        new()
        {
            TotalPrice = created.TotalPrice,
            DownPaymentAmount = created.DownPaymentAmount,
            MonthsFinanced = created.MonthsFinanced,
            ExchangeRate = created.ExchangeRate,
            StartDate = created.StartDate,
            ApplyRecalculatedSchedule = applyRecalculated,
            Installments = items,
        };

    private static List<UpdateContractScheduleItemDto> ItemsFrom(IEnumerable<Payment> payments) =>
        payments
            .OrderBy(p => p.Sequence)
            .Select(p => new UpdateContractScheduleItemDto
            {
                Sequence = p.Sequence,
                DueDate = p.DueDate,
            })
            .ToList();

    private static List<UpdateContractScheduleItemDto> ApplyItems(
        List<Payment> payments,
        int sequence,
        DateTime dueDate
    ) =>
        payments
            .OrderBy(p => p.Sequence)
            .Select(p => new UpdateContractScheduleItemDto
            {
                Sequence = p.Sequence,
                DueDate = p.Sequence == sequence ? dueDate : p.DueDate,
            })
            .ToList();

    private static async Task<List<Payment>> ActiveInstallmentsAsync(
        DatabaseContext context,
        Guid contractId
    ) =>
        await context
            .Payments.Where(p =>
                p.ContractId == contractId && p.Type == PaymentType.Installment && p.IsActive
            )
            .OrderBy(p => p.Sequence)
            .ToListAsync();

    private static async Task AddReceiptAsync(
        DatabaseContext context,
        Guid contractId,
        Guid paymentId,
        decimal amount
    )
    {
        var transaction = new PaymentTransaction
        {
            PaymentDate = DateTimeOffset.UtcNow,
            AmountPaid = amount,
            ContractId = contractId,
            PaymentMethod = PaymentMethod.CASH,
            ReferenceNumber = "TEST-RECEIPT",
        };
        context.PaymentTransactions.Add(transaction);
        await context.SaveChangesAsync();
        context.PaymentTransactionPayments.Add(
            new PaymentTransactionPayment
            {
                PaymentTransactionId = transaction.Id,
                PaymentId = paymentId,
                AmountPaid = amount,
            }
        );
        await context.SaveChangesAsync();
    }

    private static async Task MarkFirstInstallmentPaidAsync(DatabaseContext context, Guid contractId) =>
        await MarkInstallmentPaidAsync(context, contractId, 1);

    private static async Task MarkInstallmentPaidAsync(
        DatabaseContext context,
        Guid contractId,
        int sequence
    )
    {
        var installment = await context.Payments.SingleAsync(p =>
            p.ContractId == contractId && p.Type == PaymentType.Installment && p.Sequence == sequence
        );
        installment.Paid = true;
        await context.SaveChangesAsync();
    }

    private static DateTime UtcDate(DateTime value) =>
        DateTime.SpecifyKind(value.ToUniversalTime().Date, DateTimeKind.Utc);

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
