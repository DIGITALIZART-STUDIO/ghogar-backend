using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;
using GestionHogar.Utils;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Services;

public class ContractService(
    DatabaseContext context,
    IPaymentService paymentService,
    IProjectContractService? projectContractService = null,
    OdsTemplateService? odsTemplateService = null,
    SofficeConverterService? sofficeConverterService = null
) : IContractService
{
    public async Task<PaginatedResponseV2<ContractDto>> GetPaginatedAsync(
        int page,
        int pageSize,
        PaginationService paginationService,
        string? search = null,
        ContractStatus[]? status = null,
        ContractOrigin[]? origin = null,
        Guid? clientId = null,
        Guid? projectId = null,
        bool? overdue = null,
        bool? hasBalance = null,
        string? orderBy = null
    )
    {
        var query = context
            .Contracts.Include(c => c.Client)
            .Include(c => c.Lot)
            .ThenInclude(l => l.Block)
            .ThenInclude(b => b.Project)
            .Include(c => c.Quotation)
            .Include(c => c.Payments)
            .Where(c => c.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(c =>
                c.Code.ToLower().Contains(term)
                || (c.Client.Name != null && c.Client.Name.ToLower().Contains(term))
                || (c.Client.CompanyName != null && c.Client.CompanyName.ToLower().Contains(term))
                || (c.Client.PhoneNumber != null && c.Client.PhoneNumber.Contains(term))
                || (c.Client.Dni != null && c.Client.Dni.Contains(term))
                || (c.Client.Ruc != null && c.Client.Ruc.Contains(term))
                || (c.Quotation != null && c.Quotation.Code.ToLower().Contains(term))
                || c.Lot.LotNumber.ToLower().Contains(term)
            );
        }

        if (status != null && status.Length > 0)
        {
            query = query.Where(c => status.Contains(c.Status));
        }

        if (origin != null && origin.Length > 0)
        {
            query = query.Where(c => origin.Contains(c.Origin));
        }

        if (clientId.HasValue)
        {
            query = query.Where(c => c.ClientId == clientId.Value);
        }

        if (projectId.HasValue)
        {
            query = query.Where(c => c.Lot.Block.ProjectId == projectId.Value);
        }

        if (overdue == true)
        {
            var now = DateTime.UtcNow;
            query = query.Where(c =>
                c.Payments.Any(p => p.IsActive && !p.Paid && p.DueDate < now)
            );
        }

        if (hasBalance == true)
        {
            query = query.Where(c =>
                c.Status != ContractStatus.Rescinded
                && c.Payments.Any(p => p.IsActive && !p.Paid)
            );
        }

        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            var parts = orderBy.Split(' ');
            var field = parts[0].ToLower();
            var desc = parts.Length > 1 && parts[1].ToLower() == "desc";

            query = field switch
            {
                "code" => desc
                    ? query.OrderByDescending(c => c.Code)
                    : query.OrderBy(c => c.Code),
                "startdate" => desc
                    ? query.OrderByDescending(c => c.StartDate)
                    : query.OrderBy(c => c.StartDate),
                "status" => desc
                    ? query.OrderByDescending(c => c.Status)
                    : query.OrderBy(c => c.Status),
                "totalprice" => desc
                    ? query.OrderByDescending(c => c.TotalPrice)
                    : query.OrderBy(c => c.TotalPrice),
                "createdat" => desc
                    ? query.OrderByDescending(c => c.CreatedAt)
                    : query.OrderBy(c => c.CreatedAt),
                _ => query.OrderByDescending(c => c.CreatedAt),
            };
        }
        else
        {
            query = query.OrderByDescending(c => c.CreatedAt);
        }

        return await paginationService.PaginateAsync(query.Select(MapToDtoExpression), page, pageSize);
    }

    public async Task<ContractDto?> GetByIdAsync(Guid id)
    {
        var contract = await context
            .Contracts.Include(c => c.Client)
            .Include(c => c.Lot)
            .ThenInclude(l => l.Block)
            .ThenInclude(b => b.Project)
            .Include(c => c.Quotation)
            .Include(c => c.Payments)
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

        return contract == null ? null : MapToDto(contract);
    }

    public Task<IEnumerable<PaymentDto>> GetScheduleByIdAsync(Guid id)
    {
        return paymentService.GetPaymentScheduleByContractIdAsync(id);
    }

    public async Task<string> GenerateCodeAsync(int? year = null)
    {
        var y = year ?? DateTime.UtcNow.Year;
        var prefix = $"CNT-{y}-";
        var last = await context
            .Contracts.Where(c => c.Code.StartsWith(prefix))
            .OrderByDescending(c => c.Code)
            .Select(c => c.Code)
            .FirstOrDefaultAsync();

        var sequence = 1;
        if (last != null)
        {
            var parts = last.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out var lastSequence))
            {
                sequence = lastSequence + 1;
            }
        }

        return $"{prefix}{sequence:D5}";
    }

    public async Task<ContractDto> CreateFromPaidReservationAsync(Guid reservationId)
    {
        var backfill = new ContractBackfillService(context);
        await backfill.BackfillSingleReservationAsync(reservationId);

        var contract =
            await context
                .Contracts.Include(c => c.Client)
                .Include(c => c.Lot)
                .ThenInclude(l => l.Block)
                .ThenInclude(b => b.Project)
                .Include(c => c.Quotation)
                .Include(c => c.Payments)
                .FirstOrDefaultAsync(c => c.ReservationId == reservationId && c.IsActive)
            ?? throw new InvalidOperationException(
                "No se pudo crear o encontrar el contrato de la separación"
            );

        return MapToDto(contract);
    }

    public async Task RescindIfLeavingPaidReservationAsync(Guid reservationId)
    {
        var contract = await context
            .Contracts.Include(c => c.PaymentTransactions)
            .FirstOrDefaultAsync(c => c.ReservationId == reservationId && c.IsActive);

        if (contract == null)
        {
            return;
        }

        var hasReceipts = contract.PaymentTransactions.Any(t => t.IsActive);
        if (contract.Status == ContractStatus.Active && hasReceipts)
        {
            throw new InvalidOperationException(
                "No se puede revertir la separación: el contrato está activo y tiene recibos. Rescinda el contrato primero."
            );
        }

        contract.Status = ContractStatus.Rescinded;
        contract.IsActive = false;
        contract.ModifiedAt = DateTime.UtcNow;
    }

    public async Task<IEnumerable<ContractDto>> GetByClientIdAsync(Guid clientId)
    {
        var contracts = await context
            .Contracts.Include(c => c.Client)
            .Include(c => c.Lot)
            .ThenInclude(l => l.Block)
            .ThenInclude(b => b.Project)
            .Include(c => c.Quotation)
            .Include(c => c.Payments)
            .Where(c => c.IsActive && c.ClientId == clientId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return contracts.Select(MapToDto);
    }

    public async Task<ClientFinancialSummaryDto> GetFinancialSummaryByClientIdAsync(Guid clientId)
    {
        var contracts = await context
            .Contracts.Include(c => c.Payments)
            .Where(c => c.IsActive && c.ClientId == clientId)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var activeStatuses = new[]
        {
            ContractStatus.Draft,
            ContractStatus.PendingValidation,
            ContractStatus.Active,
        };
        var openContracts = contracts
            .Where(c => c.Status != ContractStatus.Rescinded)
            .ToList();
        var unpaid = openContracts
            .SelectMany(c => c.Payments.Where(p => p.IsActive && !p.Paid))
            .ToList();

        return new ClientFinancialSummaryDto
        {
            ActiveContracts = contracts.Count(c => activeStatuses.Contains(c.Status)),
            NextPaymentDueDate = unpaid
                .OrderBy(p => p.DueDate)
                .Select(p => (DateTime?)p.DueDate)
                .FirstOrDefault(),
            Balances = openContracts
                .GroupBy(c => c.Currency)
                .Select(group =>
                {
                    var payments = group.SelectMany(c => c.Payments.Where(p => p.IsActive)).ToList();
                    var unpaidInCurrency = payments.Where(p => !p.Paid).ToList();
                    return new ClientCurrencyBalanceDto
                    {
                        Currency = group.Key,
                        RemainingAmount = unpaidInCurrency.Sum(p => p.AmountDue),
                        OverdueAmount = unpaidInCurrency
                            .Where(p => p.DueDate < now)
                            .Sum(p => p.AmountDue),
                        TotalPaid = payments.Where(p => p.Paid).Sum(p => p.AmountDue),
                    };
                })
                .OrderBy(balance => balance.Currency)
                .ToList(),
        };
    }

    public async Task<ContractDto> CreateAsync(CreateContractDto dto)
    {
        if (dto.Origin is not (ContractOrigin.Direct or ContractOrigin.Legacy))
        {
            throw new ArgumentException(
                "Solo se puede dar de alta un contrato Directo o Legado. El origen Pipeline nace al marcar la separación como pagada."
            );
        }

        if (dto.DownPaymentAmount >= dto.TotalPrice)
        {
            throw new ArgumentException("La inicial debe ser menor al precio total.");
        }

        if (dto.Origin == ContractOrigin.Legacy)
        {
            if (!dto.StartDate.HasValue)
            {
                throw new ArgumentException("El contrato legado requiere fecha de inicio.");
            }

            if (dto.PaidInstallments < 0 || dto.PaidInstallments > dto.MonthsFinanced)
            {
                throw new ArgumentException(
                    "Las cuotas ya pagadas deben estar entre 0 y el número de meses financiados."
                );
            }
        }

        var client =
            await context.Clients.FirstOrDefaultAsync(c => c.Id == dto.ClientId && c.IsActive)
            ?? throw new ArgumentException("Cliente no encontrado");

        var lot =
            await context
                .Lots.Include(l => l.Block)
                .ThenInclude(b => b.Project)
                .FirstOrDefaultAsync(l => l.Id == dto.LotId && l.IsActive)
            ?? throw new ArgumentException("Lote no encontrado");

        var lotTaken = await context.Contracts.AnyAsync(c =>
            c.LotId == dto.LotId
            && c.IsActive
            && c.Status != ContractStatus.Completed
            && c.Status != ContractStatus.Rescinded
        );
        if (lotTaken)
        {
            throw new InvalidOperationException(
                "El lote ya tiene un contrato activo. Rescinda o complete el existente antes de crear otro."
            );
        }

        if (dto.Origin == ContractOrigin.Direct)
        {
            if (lot.Status is not (LotStatus.Available or LotStatus.Quoted))
            {
                throw new InvalidOperationException(
                    $"El lote no está disponible para un contrato directo. Estado actual: {lot.Status}."
                );
            }
        }

        var project =
            lot.Block?.Project
            ?? throw new ArgumentException("El lote no tiene un proyecto asociado");
        var currency = CurrencyMapping.FromIsoCode(project.Currency);

        var startDate = DateTime.SpecifyKind(
            (dto.StartDate ?? DateTime.UtcNow).ToUniversalTime(),
            DateTimeKind.Utc
        );
        var amountFinanced = dto.TotalPrice - dto.DownPaymentAmount;
        var isLegacy = dto.Origin == ContractOrigin.Legacy;

        var contract = new Contract
        {
            Code = await GenerateCodeAsync(startDate.Year),
            ClientId = client.Id,
            Client = client,
            LotId = lot.Id,
            Lot = lot,
            Origin = dto.Origin,
            Status = isLegacy ? ContractStatus.Active : ContractStatus.PendingValidation,
            TotalPrice = dto.TotalPrice,
            DownPaymentAmount = dto.DownPaymentAmount,
            AmountFinanced = amountFinanced,
            MonthsFinanced = dto.MonthsFinanced,
            Currency = currency,
            ExchangeRate = dto.ExchangeRate,
            StartDate = startDate,
            SignedAt = isLegacy ? startDate : null,
            CoOwners = dto.CoOwners,
            IsActive = true,
        };

        lot.Status = LotStatus.Reserved;
        context.Contracts.Add(contract);
        await context.SaveChangesAsync();

        GenerateSchedule(
            contract,
            paidInstallments: isLegacy ? dto.PaidInstallments : 0,
            downPaymentPaid: isLegacy
        );
        await context.SaveChangesAsync();

        return await GetByIdAsync(contract.Id)
            ?? throw new InvalidOperationException("No se pudo cargar el contrato creado");
    }

    public async Task<ContractDto> UpdateAsync(Guid contractId, UpdateContractDto dto)
    {
        if (dto.DownPaymentAmount >= dto.TotalPrice)
        {
            throw new ArgumentException("La inicial debe ser menor al precio total.");
        }

        var contract =
            await context
                .Contracts.Include(c => c.Client)
                .Include(c => c.Lot)
                .ThenInclude(l => l.Block)
                .ThenInclude(b => b.Project)
                .Include(c => c.Quotation)
                .Include(c => c.Payments)
                .FirstOrDefaultAsync(c => c.Id == contractId && c.IsActive)
            ?? throw new ArgumentException("Contrato no encontrado");

        if (contract.Status is ContractStatus.Completed or ContractStatus.Rescinded)
        {
            throw new InvalidOperationException(
                "No se puede editar un contrato completado o rescindido."
            );
        }

        var startDate = dto.StartDate.HasValue
            ? DateTime.SpecifyKind(dto.StartDate.Value.ToUniversalTime(), DateTimeKind.Utc)
            : contract.StartDate;

        var activePayments = contract.Payments.Where(p => p.IsActive).ToList();
        var paymentIds = activePayments.Select(p => p.Id).ToList();
        var paymentsWithReceipts = await context
            .PaymentTransactionPayments.Where(d => paymentIds.Contains(d.PaymentId))
            .Select(d => d.PaymentId)
            .Distinct()
            .ToListAsync();
        var receiptSet = paymentsWithReceipts.ToHashSet();

        bool IsLocked(Payment p) => p.Paid || receiptSet.Contains(p.Id);

        var lockedInstallments = activePayments
            .Where(p => p.Type == PaymentType.Installment && IsLocked(p))
            .OrderBy(p => p.Sequence)
            .ToList();

        if (dto.MonthsFinanced < lockedInstallments.Count)
        {
            throw new InvalidOperationException(
                $"El plazo no puede ser menor a las {lockedInstallments.Count} cuotas ya pagadas o con recibos."
            );
        }

        var lockedInstallmentSum = lockedInstallments.Sum(p => p.AmountDue);
        var amountFinanced = dto.TotalPrice - dto.DownPaymentAmount;
        var remainingFinanced = amountFinanced - lockedInstallmentSum;
        var remainingMonths = dto.MonthsFinanced - lockedInstallments.Count;

        if (remainingFinanced < -0.01m)
        {
            throw new InvalidOperationException(
                "El nuevo precio e inicial dejan un financiamiento menor a lo ya pagado en cuotas."
            );
        }

        if (remainingMonths == 0 && remainingFinanced > 0.01m)
        {
            throw new InvalidOperationException(
                "Queda saldo por financiar pero no hay meses libres. Aumenta el plazo o ajusta los montos."
            );
        }

        var scheduleChanged =
            contract.TotalPrice != dto.TotalPrice
            || contract.DownPaymentAmount != dto.DownPaymentAmount
            || contract.MonthsFinanced != dto.MonthsFinanced
            || contract.StartDate != startDate;

        contract.TotalPrice = dto.TotalPrice;
        contract.DownPaymentAmount = dto.DownPaymentAmount;
        contract.AmountFinanced = amountFinanced;
        contract.MonthsFinanced = dto.MonthsFinanced;
        contract.ExchangeRate = dto.ExchangeRate;
        contract.StartDate = startDate;
        contract.CoOwners = dto.CoOwners;
        contract.ModifiedAt = DateTime.UtcNow;

        if (scheduleChanged)
        {
            RebuildUnpaidSchedule(contract, activePayments, IsLocked, remainingFinanced, remainingMonths);
        }

        await context.SaveChangesAsync();
        return await GetByIdAsync(contract.Id)
            ?? throw new InvalidOperationException("No se pudo cargar el contrato actualizado");
    }

    public async Task<ContractDto> RescindAsync(Guid contractId)
    {
        var contract =
            await context
                .Contracts.Include(c => c.Client)
                .Include(c => c.Lot)
                .ThenInclude(l => l.Block)
                .ThenInclude(b => b.Project)
                .Include(c => c.Quotation)
                .Include(c => c.Payments)
                .Include(c => c.Reservation)
                .ThenInclude(r => r!.Quotation)
                .ThenInclude(q => q!.Lot)
                .FirstOrDefaultAsync(c => c.Id == contractId && c.IsActive)
            ?? throw new ArgumentException("Contrato no encontrado");

        if (contract.Status is ContractStatus.Completed or ContractStatus.Rescinded)
        {
            throw new InvalidOperationException(
                "El contrato ya está cerrado y no se puede anular."
            );
        }

        contract.Status = ContractStatus.Rescinded;
        contract.ModifiedAt = DateTime.UtcNow;

        var lotOccupiedByAnother = await context.Contracts.AnyAsync(c =>
            c.Id != contract.Id
            && c.LotId == contract.LotId
            && c.IsActive
            && c.Status != ContractStatus.Completed
            && c.Status != ContractStatus.Rescinded
        );

        if (!lotOccupiedByAnother && contract.Lot != null)
        {
            contract.Lot.Status = LotStatus.Available;
            contract.Lot.ModifiedAt = DateTime.UtcNow;
        }

        if (contract.Reservation != null && contract.Reservation.Status != ReservationStatus.ANULATED)
        {
            contract.Reservation.Status = ReservationStatus.ANULATED;
            contract.Reservation.ModifiedAt = DateTime.UtcNow;
            if (contract.Reservation.Quotation?.Lot != null && !lotOccupiedByAnother)
            {
                contract.Reservation.Quotation.Lot.Status = LotStatus.Available;
                contract.Reservation.Quotation.Lot.ModifiedAt = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync();
        return await GetByIdAsync(contract.Id)
            ?? throw new InvalidOperationException("No se pudo cargar el contrato anulado");
    }

    public async Task<ContractDto> ToggleValidationStatusAsync(Guid contractId)
    {
        var contract =
            await context
                .Contracts.Include(c => c.Reservation)
                .Include(c => c.Client)
                .Include(c => c.Lot)
                .ThenInclude(l => l.Block)
                .ThenInclude(b => b.Project)
                .Include(c => c.Quotation)
                .Include(c => c.Payments)
                .FirstOrDefaultAsync(c => c.Id == contractId && c.IsActive)
            ?? throw new ArgumentException("Contrato no encontrado");

        if (contract.Status is ContractStatus.Completed or ContractStatus.Rescinded)
        {
            throw new InvalidOperationException(
                "No se puede cambiar la validación de un contrato completado o rescindido."
            );
        }

        if (contract.Status is ContractStatus.PendingValidation or ContractStatus.Draft)
        {
            contract.Status = ContractStatus.Active;
            contract.SignedAt ??= DateTime.UtcNow;
            if (contract.Reservation != null)
            {
                contract.Reservation.ContractValidationStatus = ContractValidationStatus.Validated;
            }
        }
        else if (contract.Status == ContractStatus.Active)
        {
            contract.Status = ContractStatus.PendingValidation;
            contract.SignedAt = null;
            if (contract.Reservation != null)
            {
                contract.Reservation.ContractValidationStatus =
                    ContractValidationStatus.PendingValidation;
            }
        }

        contract.ModifiedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return MapToDto(contract);
    }

    public async Task<byte[]> GenerateContractPdfAsync(Guid contractId)
    {
        var docxBytes = await GenerateContractDocxAsync(contractId);
        var converter =
            sofficeConverterService
            ?? throw new InvalidOperationException("Conversor PDF no configurado");
        var (pdfBytes, pdfError) = converter.ConvertToPdf(docxBytes, "docx");
        if (pdfError != null)
            throw new ArgumentException($"Error al convertir contrato a PDF: {pdfError}");
        return pdfBytes;
    }

    public async Task<byte[]> GenerateContractDocxAsync(Guid contractId)
    {
        var renderer =
            projectContractService
            ?? throw new InvalidOperationException("Servicio de plantilla de contrato no configurado");

        var contract =
            await context
                .Contracts.Include(c => c.Client)
                .Include(c => c.Lot)
                .ThenInclude(l => l.Block)
                .ThenInclude(b => b.Project)
                .Include(c => c.Quotation)
                .Include(c => c.Reservation)
                .FirstOrDefaultAsync(c => c.Id == contractId && c.IsActive)
            ?? throw new ArgumentException("Contrato no encontrado");

        var client = contract.Client;
        var quotation = contract.Quotation;
        var project = contract.Lot?.Block?.Project;
        var lot = contract.Lot;
        var block = contract.Lot?.Block;

        if (project == null)
            throw new ArgumentException("El contrato no tiene un proyecto asociado");

        var allCoOwnersText = FormatCoOwners(contract.CoOwners);
        var area = quotation?.AreaAtQuotation ?? lot?.Area ?? 0;
        var pricePerM2 = quotation?.PricePerM2AtQuotation ?? (area > 0 ? contract.TotalPrice / area : 0);
        var startDate = contract.SignedAt ?? contract.StartDate;
        var currency = contract.Currency;

        var placeholders = new Dictionary<string, string>()
        {
            { "{nro_contrato}", contract.Code },
            { "{honorifico_cliente}", client?.Type == ClientType.Natural ? "Sr./Sra." : "Empresa" },
            { "{nombre_cliente}", client?.Name ?? client?.CompanyName ?? "" },
            { "{dni_cliente}", client?.Dni ?? client?.Ruc ?? "" },
            { "{estado_civil_cliente}", "" },
            { "{ocupacion_cliente}", "" },
            { "{domicilio_cliente}", client?.Address ?? "" },
            { "{distrito_cliente}", "" },
            { "{provincia_cliente}", "" },
            { "{departamento_cliente}", client?.Country ?? "" },
            { "{nombre_proyecto}", project.Name ?? "" },
            { "{nro_lote}", lot?.LotNumber ?? "" },
            { "{manzana}", block?.Name ?? "" },
            { "{precio_dolares_metro_cuadrado}", CurrencyMapping.FormatAmount(pricePerM2, currency) },
            { "{area_terreno}", $"{area:N2}" },
            { "{precio_departamento_dolares}", CurrencyMapping.FormatAmount(contract.TotalPrice, currency) },
            {
                "{precio_departamento_dolares_letras}",
                ConvertAmountToWords(contract.TotalPrice, currency)
            },
            { "{precio_cochera_dolares}", "" },
            { "{precio_cochera_dolares_letras}", "" },
            { "{area_cochera}", "" },
            { "{nro_signada_cochera}", "" },
            { "{precio_total_dolares}", CurrencyMapping.FormatAmount(contract.TotalPrice, currency) },
            {
                "{precio_total_dolares_letras}",
                ConvertAmountToWords(contract.TotalPrice, currency)
            },
            { "{precio_inicial_dolares}", CurrencyMapping.FormatAmount(contract.DownPaymentAmount, currency) },
            {
                "{precio_inicial_dolares_letras}",
                ConvertAmountToWords(contract.DownPaymentAmount, currency)
            },
            {
                "{fecha_suscripcion_contrato_letras}",
                ConvertDateToWords(startDate)
            },
            { "{representantes_legales}", allCoOwnersText },
        };

        return await renderer.RenderFilledDocxAsync(project.Id, placeholders);
    }

    public async Task<byte[]> GenerateSchedulePdfAsync(Guid contractId)
    {
        var ods =
            odsTemplateService
            ?? throw new InvalidOperationException("Servicio de plantilla ODS no configurado");
        var converter =
            sofficeConverterService
            ?? throw new InvalidOperationException("Conversor PDF no configurado");

        var contract =
            await context
                .Contracts.Include(c => c.Client)
                .Include(c => c.Lot)
                .ThenInclude(l => l.Block)
                .ThenInclude(b => b.Project)
                .Include(c => c.Payments)
                .FirstOrDefaultAsync(c => c.Id == contractId && c.IsActive)
            ?? throw new ArgumentException("Contrato no encontrado");

        var client = contract.Client;
        var project = contract.Lot?.Block?.Project;
        var allPayments = contract
            .Payments.Where(p => p.IsActive && p.Type != PaymentType.DownPayment)
            .OrderBy(p => p.Sequence)
            .ThenBy(p => p.DueDate)
            .ToList();

        var currencySymbol = CurrencyMapping.Symbol(contract.Currency);
        var staticPlaceholders = new Dictionary<string, string>()
        {
            { "{NOMBRE_PROYECTO}", project?.Name?.ToUpper() ?? "" },
            { "{nombre_cliente}", client?.Name ?? client?.CompanyName ?? "" },
            { "{lote_cliente}", contract.Lot?.LotNumber ?? "" },
            { "{total_inicial}", $"{currencySymbol} {contract.DownPaymentAmount:N2}" },
            { "{total_credito}", $"{currencySymbol} {contract.AmountFinanced:N2}" },
            { "{total_venta}", $"{currencySymbol} {contract.TotalPrice:N2}" },
            { "{nro_cuotas}", allPayments.Count.ToString() },
        };

        var dynamicRowsData = new List<Dictionary<string, string>>();
        decimal remainingAmount = contract.AmountFinanced;
        for (int i = 0; i < allPayments.Count; i++)
        {
            var payment = allPayments[i];
            remainingAmount -= payment.AmountDue;
            dynamicRowsData.Add(
                new Dictionary<string, string>
                {
                    { "{nro_cuota}", (i + 1).ToString() },
                    { "{fecha_pago_cuota}", payment.DueDate.ToString("dd/MM/yyyy") },
                    { "{monto_restante}", $"{currencySymbol} {remainingAmount:N2}" },
                    { "{monto_cuota}", $"{currencySymbol} {payment.AmountDue:N2}" },
                }
            );
        }

        var templateBytes = await File.ReadAllBytesAsync("Templates/cronograma_de_pagos.ods");
        var (filledBytes, fillError) = ods.ReplacePlaceholdersWithDynamicRows(
            templateBytes,
            staticPlaceholders,
            dynamicRowsData,
            13
        );
        if (fillError != null)
            throw new ArgumentException($"Error al procesar plantilla ODS: {fillError}");

        var (pdfBytes, pdfError) = converter.ConvertToPdf(filledBytes, "ods");
        if (pdfError != null)
            throw new ArgumentException($"Error al convertir ODS a PDF: {pdfError}");

        return pdfBytes;
    }

    private void RebuildUnpaidSchedule(
        Contract contract,
        List<Payment> activePayments,
        Func<Payment, bool> isLocked,
        decimal remainingFinanced,
        int remainingMonths
    )
    {
        var toRemove = activePayments
            .Where(p =>
                p.Type is PaymentType.DownPayment or PaymentType.Installment && !isLocked(p)
            )
            .ToList();
        foreach (var payment in toRemove)
        {
            payment.IsActive = false;
            payment.ModifiedAt = DateTime.UtcNow;
        }

        var hasLockedDownPayment = activePayments.Any(p =>
            p.Type == PaymentType.DownPayment && p.IsActive && isLocked(p)
        );
        if (!hasLockedDownPayment)
        {
            context.Payments.Add(
                new Payment
                {
                    ReservationId = contract.ReservationId,
                    ContractId = contract.Id,
                    Contract = contract,
                    Type = PaymentType.DownPayment,
                    Sequence = 0,
                    DueDate = contract.StartDate,
                    AmountDue = contract.DownPaymentAmount,
                    Paid = false,
                }
            );
        }

        var lockedInstallments = activePayments
            .Where(p => p.Type == PaymentType.Installment && p.IsActive && isLocked(p))
            .ToList();
        var nextSequence =
            lockedInstallments.Count == 0 ? 1 : lockedInstallments.Max(p => p.Sequence) + 1;

        if (remainingMonths <= 0 || remainingFinanced <= 0.01m)
        {
            return;
        }

        var monthly = Math.Round(remainingFinanced / remainingMonths, 2, MidpointRounding.AwayFromZero);
        var allocated = 0m;
        for (var i = 0; i < remainingMonths; i++)
        {
            var amountDue =
                i == remainingMonths - 1
                    ? Math.Round(remainingFinanced - allocated, 2, MidpointRounding.AwayFromZero)
                    : monthly;
            allocated += amountDue;
            context.Payments.Add(
                new Payment
                {
                    ReservationId = contract.ReservationId,
                    ContractId = contract.Id,
                    Contract = contract,
                    Type = PaymentType.Installment,
                    Sequence = nextSequence + i,
                    DueDate = DateTime.SpecifyKind(
                        contract.StartDate.AddMonths(nextSequence + i),
                        DateTimeKind.Utc
                    ),
                    AmountDue = amountDue,
                    Paid = false,
                }
            );
        }
    }

    private void GenerateSchedule(Contract contract, int paidInstallments, bool downPaymentPaid)
    {
        context.Payments.Add(
            new Payment
            {
                ReservationId = contract.ReservationId,
                ContractId = contract.Id,
                Contract = contract,
                Type = PaymentType.DownPayment,
                Sequence = 0,
                DueDate = contract.StartDate,
                AmountDue = contract.DownPaymentAmount,
                Paid = downPaymentPaid,
            }
        );

        if (contract.MonthsFinanced <= 0)
        {
            return;
        }

        var monthlyPayment = Math.Round(
            contract.AmountFinanced / contract.MonthsFinanced,
            2,
            MidpointRounding.AwayFromZero
        );
        var allocated = 0m;
        for (var i = 1; i <= contract.MonthsFinanced; i++)
        {
            var amountDue =
                i == contract.MonthsFinanced
                    ? Math.Round(
                        contract.AmountFinanced - allocated,
                        2,
                        MidpointRounding.AwayFromZero
                    )
                    : monthlyPayment;
            allocated += amountDue;
            context.Payments.Add(
                new Payment
                {
                    ReservationId = contract.ReservationId,
                    ContractId = contract.Id,
                    Contract = contract,
                    Type = PaymentType.Installment,
                    Sequence = i,
                    DueDate = DateTime.SpecifyKind(contract.StartDate.AddMonths(i), DateTimeKind.Utc),
                    AmountDue = amountDue,
                    Paid = i <= paidInstallments,
                }
            );
        }
    }

    private static string FormatCoOwners(string? coOwnersJson)
    {
        if (string.IsNullOrEmpty(coOwnersJson))
            return "";

        try
        {
            var coOwners = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                coOwnersJson
            );
            if (coOwners.ValueKind != System.Text.Json.JsonValueKind.Array || coOwners.GetArrayLength() == 0)
                return "";

            var coOwnersList = new List<string>();
            for (int i = 0; i < coOwners.GetArrayLength(); i++)
            {
                var coOwner = coOwners[i];
                var name = coOwner.TryGetProperty("name", out var nameElement)
                    ? nameElement.GetString() ?? ""
                    : "";
                var dni = coOwner.TryGetProperty("dni", out var dniElement)
                    ? dniElement.GetString() ?? ""
                    : "";
                var address = coOwner.TryGetProperty("address", out var addressElement)
                    ? addressElement.GetString() ?? ""
                    : "";

                if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(dni))
                    continue;

                var coOwnerText = name;
                if (!string.IsNullOrEmpty(dni))
                {
                    coOwnerText += string.IsNullOrEmpty(coOwnerText)
                        ? $"identificado con DNI N° {dni}"
                        : $", identificado con DNI N° {dni}";
                }
                if (!string.IsNullOrEmpty(address))
                {
                    coOwnerText += string.IsNullOrEmpty(coOwnerText)
                        ? $"domiciliado en {address}"
                        : $", domiciliado en {address}";
                }
                coOwnersList.Add(coOwnerText);
            }

            if (coOwnersList.Count == 0)
                return "";

            string formatted = coOwnersList.Count switch
            {
                1 => coOwnersList[0],
                2 => $"{coOwnersList[0]} y {coOwnersList[1]}",
                _ => $"{string.Join("; ", coOwnersList.Take(coOwnersList.Count - 1))}; y {coOwnersList.Last()}",
            };
            return $", y sus representantes legales: {formatted}";
        }
        catch
        {
            return "";
        }
    }

    private static string ConvertDateToWords(DateTime date)
    {
        var months = new[]
        {
            "",
            "enero",
            "febrero",
            "marzo",
            "abril",
            "mayo",
            "junio",
            "julio",
            "agosto",
            "septiembre",
            "octubre",
            "noviembre",
            "diciembre",
        };
        var dayWords = ConvertNumberToWords(date.Day).ToLower();
        var monthWord = months[date.Month];
        var yearWords = ConvertNumberToWords(date.Year).ToLower();
        return $"{dayWords} de {monthWord} del año {yearWords}";
    }

    private static string ConvertAmountToWords(decimal amount, Currency currency)
    {
        var currencyName = CurrencyMapping.NameInWords(currency);
        var integerPart = (int)Math.Floor(amount);
        var decimalPart = (int)Math.Round((amount - integerPart) * 100);
        var words = ConvertNumberToWords(integerPart);
        words += decimalPart > 0 ? $" CON {decimalPart:00}/100" : " CON 00/100";
        return $"{words} {currencyName}";
    }

    private static string ConvertNumberToWords(int number)
    {
        if (number == 0)
            return "CERO";

        var ones = new[]
        {
            "",
            "UNO",
            "DOS",
            "TRES",
            "CUATRO",
            "CINCO",
            "SEIS",
            "SIETE",
            "OCHO",
            "NUEVE",
        };
        var tens = new[]
        {
            "",
            "",
            "VEINTE",
            "TREINTA",
            "CUARENTA",
            "CINCUENTA",
            "SESENTA",
            "SETENTA",
            "OCHENTA",
            "NOVENTA",
        };
        var teens = new[]
        {
            "DIEZ",
            "ONCE",
            "DOCE",
            "TRECE",
            "CATORCE",
            "QUINCE",
            "DIECISÉIS",
            "DIECISIETE",
            "DIECIOCHO",
            "DIECINUEVE",
        };
        var hundreds = new[]
        {
            "",
            "CIENTO",
            "DOSCIENTOS",
            "TRESCIENTOS",
            "CUATROCIENTOS",
            "QUINIENTOS",
            "SEISCIENTOS",
            "SETECIENTOS",
            "OCHOCIENTOS",
            "NOVECIENTOS",
        };

        if (number < 10)
            return ones[number];
        if (number < 20)
            return teens[number - 10];
        if (number < 100)
        {
            var ten = number / 10;
            var one = number % 10;
            if (ten == 2 && one > 0)
                return $"VEINTI{ones[one]}";
            return tens[ten] + (one > 0 ? $" Y {ones[one]}" : "");
        }
        if (number < 1000)
        {
            var hundred = number / 100;
            var remainder = number % 100;
            var hundredText = hundred == 1 && remainder == 0 ? "CIEN" : hundreds[hundred];
            return hundredText + (remainder > 0 ? $" {ConvertNumberToWords(remainder)}" : "");
        }
        if (number < 1000000)
        {
            var thousand = number / 1000;
            var remainder = number % 1000;
            var thousandText = thousand == 1 ? "MIL" : $"{ConvertNumberToWords(thousand)} MIL";
            return thousandText + (remainder > 0 ? $" {ConvertNumberToWords(remainder)}" : "");
        }

        return number.ToString();
    }

    internal static ContractDto MapToDto(Contract c) =>
        new()
        {
            Id = c.Id,
            Code = c.Code,
            ClientId = c.ClientId,
            ClientName = ClientDisplayName(c.Client),
            LotId = c.LotId,
            LotNumber = c.Lot?.LotNumber ?? string.Empty,
            BlockName = c.Lot?.Block?.Name ?? string.Empty,
            ProjectId = c.Lot?.Block?.ProjectId ?? Guid.Empty,
            ProjectName = c.Lot?.Block?.Project?.Name ?? string.Empty,
            ReservationId = c.ReservationId,
            QuotationId = c.QuotationId,
            QuotationCode = c.Quotation?.Code,
            Origin = c.Origin,
            Status = c.Status,
            TotalPrice = c.TotalPrice,
            DownPaymentAmount = c.DownPaymentAmount,
            AmountFinanced = c.AmountFinanced,
            MonthsFinanced = c.MonthsFinanced,
            Currency = c.Currency,
            ExchangeRate = c.ExchangeRate,
            StartDate = c.StartDate,
            SignedAt = c.SignedAt,
            CoOwners = c.CoOwners,
            PaymentCount = c.Payments?.Count(p => p.IsActive) ?? 0,
            NextPaymentDueDate = c
                .Payments?.Where(p => p.IsActive && !p.Paid)
                .OrderBy(p => p.DueDate)
                .Select(p => (DateTime?)p.DueDate)
                .FirstOrDefault(),
            NextPaymentAmount =
                c.Payments?.Where(p => p.IsActive && !p.Paid)
                    .OrderBy(p => p.DueDate)
                    .Select(p => p.AmountDue)
                    .FirstOrDefault() ?? 0,
            AmountPaid = c.Payments?.Where(p => p.IsActive && p.Paid).Sum(p => p.AmountDue) ?? 0,
            RemainingAmount =
                c.Payments?.Where(p => p.IsActive && !p.Paid).Sum(p => p.AmountDue) ?? 0,
            CreatedAt = c.CreatedAt,
            ModifiedAt = c.ModifiedAt,
        };

    private static string ClientDisplayName(Client? client)
    {
        if (client == null)
            return string.Empty;
        return client.Type == ClientType.Natural
            ? client.Name ?? string.Empty
            : client.CompanyName ?? client.Name ?? string.Empty;
    }

    // Expression-bodied projection so EF can translate the paginated query.
    private static readonly System.Linq.Expressions.Expression<
        Func<Contract, ContractDto>
    > MapToDtoExpression = c => new ContractDto
    {
        Id = c.Id,
        Code = c.Code,
        ClientId = c.ClientId,
        ClientName =
            c.Client.Type == ClientType.Natural
                ? c.Client.Name ?? string.Empty
                : c.Client.CompanyName ?? c.Client.Name ?? string.Empty,
        LotId = c.LotId,
        LotNumber = c.Lot.LotNumber,
        BlockName = c.Lot.Block.Name,
        ProjectId = c.Lot.Block.ProjectId,
        ProjectName = c.Lot.Block.Project.Name,
        ReservationId = c.ReservationId,
        QuotationId = c.QuotationId,
        QuotationCode = c.Quotation != null ? c.Quotation.Code : null,
        Origin = c.Origin,
        Status = c.Status,
        TotalPrice = c.TotalPrice,
        DownPaymentAmount = c.DownPaymentAmount,
        AmountFinanced = c.AmountFinanced,
        MonthsFinanced = c.MonthsFinanced,
        Currency = c.Currency,
        ExchangeRate = c.ExchangeRate,
        StartDate = c.StartDate,
        SignedAt = c.SignedAt,
        CoOwners = c.CoOwners,
        PaymentCount = c.Payments.Count(p => p.IsActive),
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
        AmountPaid = c.Payments.Where(p => p.IsActive && p.Paid).Sum(p => p.AmountDue),
        RemainingAmount = c.Payments.Where(p => p.IsActive && !p.Paid).Sum(p => p.AmountDue),
        CreatedAt = c.CreatedAt,
        ModifiedAt = c.ModifiedAt,
    };
}
