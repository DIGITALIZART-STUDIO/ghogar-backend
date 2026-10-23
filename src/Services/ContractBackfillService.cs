using System.Text.Json;
using System.Text.Json.Serialization;
using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;
using GestionHogar.Utils;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Services;

public interface IContractBackfillService
{
    Task<ContractBackfillResultDto> BackfillFromReservationsAsync();
    Task BackfillSingleReservationAsync(Guid reservationId);
}

public class ContractBackfillService(DatabaseContext context) : IContractBackfillService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<ContractBackfillResultDto> BackfillFromReservationsAsync()
    {
        var result = new ContractBackfillResultDto();
        var sequenceByYear = new Dictionary<int, int>();

        var reservations = await context
            .Reservations.Include(r => r.Client)
            .Include(r => r.Quotation)
            .ThenInclude(q => q!.Lot)
            .ThenInclude(l => l.Block)
            .ThenInclude(b => b.Project)
            .Include(r => r.Quotation)
            .ThenInclude(q => q!.Advisor)
            .Include(r => r.Payments)
            .Include(r => r.Contract)
            .Where(r => r.IsActive && r.Status == ReservationStatus.CANCELED)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();

        foreach (var reservation in reservations)
        {
            await ProcessReservationAsync(reservation, result, sequenceByYear);
        }

        await LinkOrphanTransactionsAsync(result);
        await context.SaveChangesAsync();
        return result;
    }

    public async Task BackfillSingleReservationAsync(Guid reservationId)
    {
        var reservation = await context
            .Reservations.Include(r => r.Client)
            .Include(r => r.Quotation)
            .ThenInclude(q => q!.Lot)
            .ThenInclude(l => l.Block)
            .ThenInclude(b => b.Project)
            .Include(r => r.Quotation)
            .ThenInclude(q => q!.Advisor)
            .Include(r => r.Payments)
            .Include(r => r.Contract)
            .FirstOrDefaultAsync(r => r.Id == reservationId && r.IsActive);

        if (reservation == null || reservation.Status != ReservationStatus.CANCELED)
        {
            throw new InvalidOperationException(
                "Solo se crea contrato cuando la separación existe y está pagada (CANCELED)"
            );
        }

        var result = new ContractBackfillResultDto();
        var sequenceByYear = new Dictionary<int, int>();
        await ProcessReservationAsync(reservation, result, sequenceByYear);
        await context.SaveChangesAsync();
    }

    private async Task ProcessReservationAsync(
        Reservation reservation,
        ContractBackfillResultDto result,
        Dictionary<int, int> sequenceByYear
    )
    {
        if (reservation.Contract != null)
        {
            ReactivateContractForReservation(reservation);
            await LinkExistingContractAsync(reservation, result);
            return;
        }

        if (reservation.Quotation?.Lot == null)
        {
            result.Warnings.Add(
                $"Reserva {reservation.Id} CANCELED sin lote/cotización; se omite."
            );
            return;
        }

        var year = reservation.CreatedAt.Year;
        var code = await NextCodeAsync(year, sequenceByYear);
        var quotation = reservation.Quotation;

        var contract = new Contract
        {
            Code = code,
            ClientId = reservation.ClientId,
            Client = reservation.Client,
            LotId = quotation.LotId,
            Lot = quotation.Lot,
            ReservationId = reservation.Id,
            Reservation = reservation,
            QuotationId = quotation.Id,
            Quotation = quotation,
            AdvisorId = quotation.AdvisorId,
            Origin = ContractOrigin.Pipeline,
            Status = MapStatus(reservation.ContractValidationStatus),
            TotalPrice = quotation.FinalPrice,
            DownPaymentAmount = reservation.TotalAmountRequired,
            AmountFinanced = quotation.AmountFinanced,
            MonthsFinanced = quotation.MonthsFinanced,
            Currency = ResolveCurrency(reservation),
            ExchangeRate = reservation.ExchangeRate,
            StartDate = reservation.ReservationDate.ToDateTime(
                TimeOnly.MinValue,
                DateTimeKind.Utc
            ),
            CoOwners = reservation.CoOwners,
            IsActive = true,
        };

        if (reservation.ContractValidationStatus == ContractValidationStatus.Validated)
        {
            contract.SignedAt = reservation.ModifiedAt;
        }

        context.Contracts.Add(contract);
        reservation.Contract = contract;
        result.ContractsCreated++;

        await context.SaveChangesAsync();
        await EnsureInstallmentScheduleAsync(reservation, contract);
        await LinkPaymentsAndDownPaymentAsync(reservation, contract, result);
    }

    private async Task EnsureInstallmentScheduleAsync(Reservation reservation, Contract contract)
    {
        var hasInstallments = reservation.Payments.Any(p =>
            p.IsActive && p.Type != PaymentType.DownPayment
        );
        if (hasInstallments)
        {
            return;
        }

        var quotation = reservation.Quotation;
        if (quotation == null || quotation.MonthsFinanced <= 0)
        {
            return;
        }

        if (!DateTime.TryParse(quotation.QuotationDate, out var quotationDate))
        {
            quotationDate = DateTime.UtcNow;
        }

        var monthlyPayment = quotation.AmountFinanced / quotation.MonthsFinanced;
        for (var i = 1; i <= quotation.MonthsFinanced; i++)
        {
            context.Payments.Add(
                new Payment
                {
                    ReservationId = reservation.Id,
                    Reservation = reservation,
                    ContractId = contract.Id,
                    Contract = contract,
                    Type = PaymentType.Installment,
                    Sequence = i,
                    DueDate = DateTime.SpecifyKind(quotationDate.AddMonths(i), DateTimeKind.Utc),
                    AmountDue = monthlyPayment,
                    Paid = false,
                }
            );
        }

        await context.SaveChangesAsync();
        await context.Entry(reservation).Collection(r => r.Payments).LoadAsync();
    }

    private static void ReactivateContractForReservation(Reservation reservation)
    {
        var contract = reservation.Contract!;
        if (contract.IsActive && contract.Status != ContractStatus.Rescinded)
        {
            return;
        }

        contract.IsActive = true;
        contract.Status = MapStatus(reservation.ContractValidationStatus);
        contract.ModifiedAt = DateTime.UtcNow;
        if (contract.Status == ContractStatus.Active)
        {
            contract.SignedAt ??= DateTime.UtcNow;
        }
        else
        {
            contract.SignedAt = null;
        }

        var quotation = reservation.Quotation;
        if (quotation == null)
        {
            return;
        }

        contract.TotalPrice = quotation.FinalPrice;
        contract.DownPaymentAmount = reservation.TotalAmountRequired;
        contract.AmountFinanced = quotation.AmountFinanced;
        contract.MonthsFinanced = quotation.MonthsFinanced;
        contract.Currency = ResolveCurrency(reservation);
        contract.ExchangeRate = reservation.ExchangeRate;
        contract.CoOwners = reservation.CoOwners;
    }

    private async Task LinkExistingContractAsync(
        Reservation reservation,
        ContractBackfillResultDto result
    )
    {
        var contract = reservation.Contract!;
        await EnsureInstallmentScheduleAsync(reservation, contract);
        await LinkPaymentsAndDownPaymentAsync(reservation, contract, result);
    }

    private async Task LinkPaymentsAndDownPaymentAsync(
        Reservation reservation,
        Contract contract,
        ContractBackfillResultDto result
    )
    {
        var installments = reservation
            .Payments.Where(p => p.IsActive && p.Type != PaymentType.DownPayment)
            .OrderBy(p => p.DueDate)
            .ToList();

        var sequence = 1;
        foreach (var payment in installments)
        {
            var changed = false;
            if (payment.ContractId != contract.Id)
            {
                payment.ContractId = contract.Id;
                changed = true;
            }

            if (payment.Type != PaymentType.Installment)
            {
                payment.Type = PaymentType.Installment;
                changed = true;
            }

            if (payment.Sequence != sequence)
            {
                payment.Sequence = sequence;
                changed = true;
            }

            if (changed)
            {
                result.PaymentsLinked++;
            }

            sequence++;
        }

        var hasDownPayment = await context.Payments.AnyAsync(p =>
            p.ContractId == contract.Id && p.Type == PaymentType.DownPayment && p.IsActive
        );

        if (!hasDownPayment && reservation.TotalAmountRequired > 0)
        {
            var downPayment = new Payment
            {
                ReservationId = reservation.Id,
                Reservation = reservation,
                ContractId = contract.Id,
                Contract = contract,
                Type = PaymentType.DownPayment,
                Sequence = 0,
                DueDate = reservation.ReservationDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                AmountDue = reservation.TotalAmountRequired,
                Paid = false,
            };
            context.Payments.Add(downPayment);
            result.DownPaymentsCreated++;
            await context.SaveChangesAsync();
            await AllocateDownPaymentReceiptsAsync(reservation, contract, downPayment, result);
        }
        else if (hasDownPayment)
        {
            var downPayment = await context.Payments.FirstAsync(p =>
                p.ContractId == contract.Id && p.Type == PaymentType.DownPayment && p.IsActive
            );
            await AllocateDownPaymentReceiptsAsync(reservation, contract, downPayment, result);
        }

        var transactions = await context
            .PaymentTransactions.Where(t => t.ReservationId == reservation.Id && t.ContractId == null)
            .ToListAsync();

        foreach (var transaction in transactions)
        {
            transaction.ContractId = contract.Id;
            result.TransactionsLinked++;
        }
    }

    private async Task AllocateDownPaymentReceiptsAsync(
        Reservation reservation,
        Contract contract,
        Payment downPayment,
        ContractBackfillResultDto result
    )
    {
        var alreadyAllocated = await context.PaymentTransactionPayments.AnyAsync(ptp =>
            ptp.PaymentId == downPayment.Id
        );
        if (alreadyAllocated)
        {
            await RefreshDownPaymentPaidFlagAsync(downPayment);
            return;
        }

        var history = DeserializeHistory(reservation.PaymentHistory);
        var confirmed = history.Where(h => h.Status == PaymentStatus.CONFIRMED).ToList();

        if (confirmed.Count == 0 && reservation.AmountPaid > 0)
        {
            confirmed.Add(
                new PaymentHistoryDto
                {
                    Id = Guid.NewGuid().ToString(),
                    Date = reservation.ModifiedAt,
                    Amount = reservation.AmountPaid,
                    Method = reservation.PaymentMethod,
                    BankName = reservation.BankName,
                    Status = PaymentStatus.CONFIRMED,
                    Notes = "Recibo sintético del backfill (AmountPaid sin PaymentHistory)",
                }
            );
        }

        var remaining = downPayment.AmountDue;
        foreach (var entry in confirmed)
        {
            if (entry.Amount <= 0)
            {
                continue;
            }

            var allocated = Math.Min(entry.Amount, Math.Max(remaining, 0));
            var transaction = new PaymentTransaction
            {
                PaymentDate = DateTime.SpecifyKind(
                    entry.Date.ToUniversalTime(),
                    DateTimeKind.Utc
                ),
                AmountPaid = entry.Amount,
                ReservationId = reservation.Id,
                ContractId = contract.Id,
                PaymentMethod = entry.Method,
                ReferenceNumber = entry.Reference,
            };
            context.PaymentTransactions.Add(transaction);
            await context.SaveChangesAsync();

            if (allocated > 0)
            {
                context.PaymentTransactionPayments.Add(
                    new PaymentTransactionPayment
                    {
                        PaymentTransactionId = transaction.Id,
                        PaymentId = downPayment.Id,
                        AmountPaid = allocated,
                    }
                );
                remaining -= allocated;
            }

            result.TransactionsLinked++;
        }

        await context.SaveChangesAsync();
        await RefreshDownPaymentPaidFlagAsync(downPayment);
    }

    private async Task RefreshDownPaymentPaidFlagAsync(Payment downPayment)
    {
        var paid = await context
            .PaymentTransactionPayments.Where(ptp => ptp.PaymentId == downPayment.Id)
            .SumAsync(ptp => ptp.AmountPaid);
        downPayment.Paid = paid >= downPayment.AmountDue;
    }

    private async Task LinkOrphanTransactionsAsync(ContractBackfillResultDto result)
    {
        var orphans = await context
            .PaymentTransactions.Where(t => t.ReservationId != null && t.ContractId == null)
            .ToListAsync();

        foreach (var orphan in orphans)
        {
            result.Warnings.Add(
                $"PaymentTransaction {orphan.Id} tiene ReservationId {orphan.ReservationId} sin contrato; no se borra."
            );
        }
    }

    private async Task<string> NextCodeAsync(int year, Dictionary<int, int> sequenceByYear)
    {
        if (!sequenceByYear.TryGetValue(year, out var sequence))
        {
            var prefix = $"CNT-{year}-";
            var last = await context
                .Contracts.Where(c => c.Code.StartsWith(prefix))
                .OrderByDescending(c => c.Code)
                .Select(c => c.Code)
                .FirstOrDefaultAsync();

            sequence = 0;
            if (last != null)
            {
                var parts = last.Split('-');
                if (parts.Length == 3 && int.TryParse(parts[2], out var lastSequence))
                {
                    sequence = lastSequence;
                }
            }
        }

        sequence++;
        sequenceByYear[year] = sequence;
        return $"CNT-{year}-{sequence:D5}";
    }

    private static Currency ResolveCurrency(Reservation reservation)
    {
        var quotationCurrency = reservation.Quotation?.Currency;
        if (!string.IsNullOrWhiteSpace(quotationCurrency))
        {
            return CurrencyMapping.FromIsoCode(quotationCurrency, "cotización");
        }

        var projectCurrency = reservation.Quotation?.Lot?.Block?.Project?.Currency;
        if (!string.IsNullOrWhiteSpace(projectCurrency))
        {
            return CurrencyMapping.FromIsoCode(projectCurrency);
        }

        return reservation.Currency;
    }

    private static ContractStatus MapStatus(ContractValidationStatus validation) =>
        validation switch
        {
            ContractValidationStatus.Validated => ContractStatus.Active,
            ContractValidationStatus.PendingValidation => ContractStatus.PendingValidation,
            _ => ContractStatus.Draft,
        };

    private static List<PaymentHistoryDto> DeserializeHistory(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<PaymentHistoryDto>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
