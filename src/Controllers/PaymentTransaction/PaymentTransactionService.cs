using GestionHogar.Dtos;
using GestionHogar.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Services;

public class PaymentTransactionService : IPaymentTransactionService
{
    private readonly DatabaseContext _context;
    private readonly ICloudflareService _cloudflareService;

    public PaymentTransactionService(DatabaseContext context, ICloudflareService cloudflareService)
    {
        _context = context;
        _cloudflareService = cloudflareService;
    }

    public async Task<IEnumerable<PaymentTransactionDTO>> GetAllAsync()
    {
        var transactions = await _context
            .PaymentTransactions.Include(pt => pt.Payments)
            .OrderByDescending(pt => pt.CreatedAt)
            .ToListAsync();

        return transactions.Select(PaymentTransactionDTO.FromEntity);
    }

    public async Task<PaymentTransactionDTO?> GetByIdAsync(Guid id)
    {
        var transaction = await _context
            .PaymentTransactions.Include(pt => pt.Payments)
            .FirstOrDefaultAsync(pt => pt.Id == id);

        return transaction != null ? PaymentTransactionDTO.FromEntity(transaction) : null;
    }

    public async Task<IEnumerable<PaymentTransactionDTO>> GetByReservationIdAsync(
        Guid reservationId
    )
    {
        var transactions = await _context
            .PaymentTransactions.Include(pt => pt.Payments)
            .Where(pt => pt.ReservationId == reservationId)
            .OrderByDescending(pt => pt.CreatedAt)
            .ToListAsync();

        return transactions.Select(PaymentTransactionDTO.FromEntity);
    }

    public async Task<IEnumerable<PaymentTransactionDTO>> GetByContractIdAsync(Guid contractId)
    {
        var transactions = await _context
            .PaymentTransactions.Include(pt => pt.Payments)
            .Where(pt => pt.ContractId == contractId)
            .OrderByDescending(pt => pt.CreatedAt)
            .ToListAsync();

        return transactions.Select(PaymentTransactionDTO.FromEntity);
    }

    public async Task<PaymentQuotaStatusDTO> GetQuotaStatusByReservationAsync(
        Guid reservationId,
        Guid? excludeTransactionId = null
    )
    {
        var contract = await _context.Contracts.FirstOrDefaultAsync(c =>
            c.ReservationId == reservationId && c.IsActive
        );
        if (contract != null)
        {
            return await GetQuotaStatusByContractAsync(contract.Id, excludeTransactionId);
        }

        var payments = await _context
            .Payments.Include(p => p.Reservation)
            .ThenInclude(r => r!.Client)
            .Include(p => p.Reservation)
            .ThenInclude(r => r!.Quotation)
            .Where(p => p.ReservationId == reservationId)
            .OrderBy(p => p.DueDate)
            .ToListAsync();

        var transactionsQuery = _context.PaymentTransactionPayments.Where(ptp =>
            ptp.PaymentTransaction.ReservationId == reservationId
        );

        if (excludeTransactionId.HasValue)
        {
            transactionsQuery = transactionsQuery.Where(ptp =>
                ptp.PaymentTransactionId != excludeTransactionId.Value
            );
        }

        var paymentDetails = await transactionsQuery.ToListAsync();

        var pendingQuotas = new List<PaymentQuotaSimpleDTO>();
        decimal totalRemaining = 0;
        int fullyUnpaidQuotas = 0;

        foreach (var payment in payments)
        {
            // Calcular cuánto ya se ha pagado por esta cuota específica
            var totalPaidForThisPayment = paymentDetails
                .Where(ptp => ptp.PaymentId == payment.Id)
                .Sum(ptp => ptp.AmountPaid);

            // Calcular cuánto falta por pagar
            var remainingAmount = payment.AmountDue - totalPaidForThisPayment;
            bool isFullyPaid = remainingAmount <= 0;

            if (!isFullyPaid)
            {
                // Si no se ha pagado nada, es una cuota completamente impaga
                if (totalPaidForThisPayment == 0)
                {
                    fullyUnpaidQuotas++;
                }

                totalRemaining += remainingAmount;

                pendingQuotas.Add(
                    new PaymentQuotaSimpleDTO
                    {
                        Id = payment.Id,
                        ReservationId = payment.ReservationId,
                        ContractId = payment.ContractId,
                        ClientName = payment.Reservation?.Client?.Name ?? string.Empty,
                        QuotationCode = payment.Reservation?.Quotation?.Code ?? string.Empty,
                        AmountDue = remainingAmount, // Monto que falta por pagar
                        DueDate = payment.DueDate,
                        Paid = false,
                        Currency = payment.Reservation?.Currency ?? Currency.SOLES,
                    }
                );
            }
        }

        return new PaymentQuotaStatusDTO
        {
            PendingQuotas = pendingQuotas,
            MinQuotasToPay = fullyUnpaidQuotas, // Cuotas completamente impagas
            MaxQuotasToPay = pendingQuotas.Count, // Total de cuotas con algún monto pendiente
            TotalAmountRemaining = totalRemaining,
            Currency = pendingQuotas.FirstOrDefault()?.Currency ?? Currency.SOLES,
        };
    }

    public async Task<PaymentQuotaStatusDTO> GetQuotaStatusByContractAsync(
        Guid contractId,
        Guid? excludeTransactionId = null
    )
    {
        var contract =
            await _context
                .Contracts.Include(c => c.Client)
                .Include(c => c.Quotation)
                .FirstOrDefaultAsync(c => c.Id == contractId && c.IsActive)
            ?? throw new InvalidOperationException("Contrato no encontrado");

        var payments = await _context
            .Payments.Where(p => p.ContractId == contractId && p.IsActive)
            .OrderBy(p => p.Sequence)
            .ThenBy(p => p.DueDate)
            .ToListAsync();

        var transactionsQuery = _context.PaymentTransactionPayments.Where(ptp =>
            ptp.PaymentTransaction.ContractId == contractId
        );

        if (excludeTransactionId.HasValue)
        {
            transactionsQuery = transactionsQuery.Where(ptp =>
                ptp.PaymentTransactionId != excludeTransactionId.Value
            );
        }

        var paymentDetails = await transactionsQuery.ToListAsync();
        var pendingQuotas = new List<PaymentQuotaSimpleDTO>();
        decimal totalRemaining = 0;
        int fullyUnpaidQuotas = 0;
        var clientName =
            contract.Client.Type == ClientType.Natural
                ? contract.Client.Name ?? string.Empty
                : contract.Client.CompanyName ?? contract.Client.Name ?? string.Empty;
        var code = contract.Quotation?.Code ?? contract.Code;

        foreach (var payment in payments)
        {
            var totalPaidForThisPayment = paymentDetails
                .Where(ptp => ptp.PaymentId == payment.Id)
                .Sum(ptp => ptp.AmountPaid);
            if (payment.Paid)
            {
                totalPaidForThisPayment = Math.Max(totalPaidForThisPayment, payment.AmountDue);
            }

            var remainingAmount = payment.AmountDue - totalPaidForThisPayment;
            if (remainingAmount <= 0)
            {
                continue;
            }

            if (totalPaidForThisPayment == 0)
            {
                fullyUnpaidQuotas++;
            }

            totalRemaining += remainingAmount;
            pendingQuotas.Add(
                new PaymentQuotaSimpleDTO
                {
                    Id = payment.Id,
                    ReservationId = payment.ReservationId ?? contract.ReservationId,
                    ContractId = contract.Id,
                    ClientName = clientName,
                    QuotationCode = code,
                    AmountDue = remainingAmount,
                    DueDate = payment.DueDate,
                    Paid = false,
                    Currency = contract.Currency,
                }
            );
        }

        return new PaymentQuotaStatusDTO
        {
            PendingQuotas = pendingQuotas,
            MinQuotasToPay = fullyUnpaidQuotas,
            MaxQuotasToPay = pendingQuotas.Count,
            TotalAmountRemaining = totalRemaining,
            Currency = contract.Currency,
        };
    }

    public async Task<PaymentTransactionDTO> CreateAsync(
        PaymentTransactionCreateDTO dto,
        IFormFile? comprobanteImage = null
    )
    {
        var (contractId, reservationId) = await ResolveContractAndReservationAsync(
            dto.ContractId,
            dto.ReservationId
        );

        List<PaymentTransactionPayment> paymentDetails;
        List<Payment> payments;

        // Determinar qué cuotas asignar
        if (dto.PaymentIds != null && dto.PaymentIds.Any())
        {
            // Caso 1: Se proporcionaron PaymentIds específicos
            payments = await _context
                .Payments.Where(p => dto.PaymentIds.Contains(p.Id))
                .ToListAsync();

            if (payments.Count != dto.PaymentIds.Count)
                throw new InvalidOperationException("Uno o más pagos no existen");

            // Crear PaymentDetails con el monto completo para cada cuota
            paymentDetails = payments
                .Select(p => new PaymentTransactionPayment
                {
                    PaymentId = p.Id,
                    AmountPaid = p.AmountDue,
                })
                .ToList();
        }
        else
        {
            // Caso 2: No se proporcionaron PaymentIds - asignación automática
            if (!contractId.HasValue && !reservationId.HasValue)
                throw new InvalidOperationException(
                    "ContractId o ReservationId es requerido cuando no se proporcionan PaymentIds"
                );

            paymentDetails = contractId.HasValue
                ? await AutoAssignPaymentsByContractAsync(
                    contractId.Value,
                    dto.AmountPaid,
                    dto.StartFromLastCuota
                )
                : await AutoAssignPaymentsAsync(
                    reservationId!.Value,
                    dto.AmountPaid,
                    dto.StartFromLastCuota
                );

            if (!paymentDetails.Any())
                throw new InvalidOperationException(
                    "No se encontraron cuotas disponibles para asignar el pago"
                );

            // Obtener las cuotas asignadas
            var paymentIds = paymentDetails.Select(pd => pd.PaymentId).ToList();
            payments = await _context.Payments.Where(p => paymentIds.Contains(p.Id)).ToListAsync();
        }

        PaymentTransaction transaction;

        // Subir imagen del comprobante si se proporciona
        if (comprobanteImage != null)
        {
            try
            {
                // Primero crear la transacción para obtener el ID
                transaction = new PaymentTransaction
                {
                    PaymentDate = dto.PaymentDate,
                    AmountPaid = dto.AmountPaid,
                    ReservationId = reservationId,
                    ContractId = contractId,
                    PaymentMethod = dto.PaymentMethod,
                    ReferenceNumber = dto.ReferenceNumber,
                    ComprobanteUrl = null, // Se actualizará después de subir la imagen
                    Payments = payments, // Mantener para compatibilidad
                    PaymentDetails = paymentDetails, // Nueva relación detallada
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow,
                };

                _context.PaymentTransactions.Add(transaction);
                await _context.SaveChangesAsync();

                // Ahora subir la imagen usando el ID de la transacción
                var comprobanteUrl = await _cloudflareService.UploadPaymentReceiptImageAsync(
                    comprobanteImage,
                    transaction.Id.ToString()
                );

                // Actualizar la transacción con la URL de la imagen
                transaction.ComprobanteUrl = comprobanteUrl;
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Error al subir la imagen del comprobante: {ex.Message}"
                );
            }
        }
        else
        {
            // Si no hay imagen, crear la transacción normalmente
            transaction = new PaymentTransaction
            {
                PaymentDate = dto.PaymentDate,
                AmountPaid = dto.AmountPaid,
                ReservationId = reservationId,
                ContractId = contractId,
                PaymentMethod = dto.PaymentMethod,
                ReferenceNumber = dto.ReferenceNumber,
                ComprobanteUrl = dto.ComprobanteUrl,
                Payments = payments, // Mantener para compatibilidad
                PaymentDetails = paymentDetails, // Nueva relación detallada
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
            };

            _context.PaymentTransactions.Add(transaction);
            await _context.SaveChangesAsync();
        }

        // Actualizar el estado Paid de cada cuota usando la nueva lógica
        foreach (var payment in payments)
        {
            await UpdatePaymentStatusAsync(payment.Id);
        }

        var created = await _context
            .PaymentTransactions.Include(pt => pt.Payments) // Mantener para compatibilidad
            .Include(pt => pt.PaymentDetails)
            .ThenInclude(pd => pd.Payment)
            .FirstAsync(pt => pt.Id == transaction.Id);

        return PaymentTransactionDTO.FromEntity(created);
    }

    public async Task<PaymentTransactionDTO?> UpdateAsync(
        Guid id,
        PaymentTransactionUpdateDTO dto,
        IFormFile? comprobanteImage = null
    )
    {
        var transaction = await _context
            .PaymentTransactions.Include(pt => pt.Payments)
            .Include(pt => pt.PaymentDetails)
            .FirstOrDefaultAsync(pt => pt.Id == id);

        if (transaction == null)
            return null;

        var (contractId, reservationId) = await ResolveContractAndReservationAsync(
            dto.ContractId ?? transaction.ContractId,
            dto.ReservationId ?? transaction.ReservationId
        );

        List<PaymentTransactionPayment> paymentDetails;
        List<Payment> payments;

        // Determinar qué cuotas asignar
        if (dto.PaymentIds != null && dto.PaymentIds.Any())
        {
            // Caso 1: Se proporcionaron PaymentIds específicos
            payments = await _context
                .Payments.Where(p => dto.PaymentIds.Contains(p.Id))
                .ToListAsync();

            if (payments.Count != dto.PaymentIds.Count)
                throw new InvalidOperationException("Uno o más pagos no existen");

            // Crear PaymentDetails con el monto completo para cada cuota
            paymentDetails = payments
                .Select(p => new PaymentTransactionPayment
                {
                    PaymentId = p.Id,
                    AmountPaid = p.AmountDue,
                })
                .ToList();
        }
        else
        {
            // Caso 2: No se proporcionaron PaymentIds - asignación automática
            if (!contractId.HasValue && !reservationId.HasValue)
                throw new InvalidOperationException(
                    "ContractId o ReservationId es requerido cuando no se proporcionan PaymentIds"
                );

            paymentDetails = contractId.HasValue
                ? await AutoAssignPaymentsByContractAsync(contractId.Value, dto.AmountPaid)
                : await AutoAssignPaymentsAsync(reservationId!.Value, dto.AmountPaid);

            if (!paymentDetails.Any())
                throw new InvalidOperationException(
                    "No se encontraron cuotas disponibles para asignar el pago"
                );

            // Obtener las cuotas asignadas
            var paymentIds = paymentDetails.Select(pd => pd.PaymentId).ToList();
            payments = await _context.Payments.Where(p => paymentIds.Contains(p.Id)).ToListAsync();
        }

        // Manejar la actualización de la imagen del comprobante si se proporciona
        if (comprobanteImage != null)
        {
            try
            {
                transaction.ComprobanteUrl =
                    await _cloudflareService.UpdatePaymentReceiptImageAsync(
                        comprobanteImage,
                        transaction.Id.ToString(),
                        transaction.ComprobanteUrl
                    );
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Error al actualizar la imagen del comprobante: {ex.Message}"
                );
            }
        }
        else
        {
            // Si no hay nueva imagen, usar la URL del DTO si se proporciona
            transaction.ComprobanteUrl = dto.ComprobanteUrl;
        }

        // Actualizar campos básicos
        transaction.PaymentDate = dto.PaymentDate;
        transaction.AmountPaid = dto.AmountPaid;
        transaction.ReservationId = reservationId;
        transaction.ContractId = contractId;
        transaction.PaymentMethod = dto.PaymentMethod;
        transaction.ReferenceNumber = dto.ReferenceNumber;
        transaction.ModifiedAt = DateTime.UtcNow;

        // Limpiar relaciones existentes y crear nuevas
        transaction.Payments = payments; // Mantener para compatibilidad
        transaction.PaymentDetails.Clear();
        foreach (var detail in paymentDetails)
        {
            detail.PaymentTransactionId = transaction.Id;
            transaction.PaymentDetails.Add(detail);
        }

        await _context.SaveChangesAsync();

        // Actualizar el estado Paid de todas las cuotas del contrato/reserva
        var allPaymentsQuery = _context.Payments.AsQueryable();
        if (transaction.ContractId.HasValue)
        {
            allPaymentsQuery = allPaymentsQuery.Where(p => p.ContractId == transaction.ContractId);
        }
        else
        {
            allPaymentsQuery = allPaymentsQuery.Where(p =>
                p.ReservationId == transaction.ReservationId
            );
        }

        var allPayments = await allPaymentsQuery.ToListAsync();

        foreach (var payment in allPayments)
        {
            await UpdatePaymentStatusAsync(payment.Id);
        }

        var updated = await _context
            .PaymentTransactions.Include(pt => pt.Payments) // Mantener para compatibilidad
            .Include(pt => pt.PaymentDetails)
            .ThenInclude(pd => pd.Payment)
            .FirstAsync(pt => pt.Id == transaction.Id);

        return PaymentTransactionDTO.FromEntity(updated);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var transaction = await _context
            .PaymentTransactions.Include(pt => pt.Payments)
            .FirstOrDefaultAsync(pt => pt.Id == id);

        if (transaction == null)
            return false;

        var payments = transaction.Payments.ToList();

        // Eliminar imagen de Cloudflare si existe
        if (!string.IsNullOrEmpty(transaction.ComprobanteUrl))
        {
            try
            {
                await _cloudflareService.DeletePaymentTransactionFolderAsync(
                    transaction.Id.ToString()
                );
            }
            catch (Exception ex)
            {
                // Log el error pero continúa con la eliminación de la transacción
                // La imagen puede no existir o haber sido eliminada previamente
                // No lanzamos la excepción para no bloquear la eliminación de la transacción
                Console.WriteLine(
                    $"Error al eliminar imagen de Cloudflare para transacción {transaction.Id}: {ex.Message}"
                );
            }
        }

        _context.PaymentTransactions.Remove(transaction);
        await _context.SaveChangesAsync();

        // Actualizar el estado Paid de cada cuota afectada usando la nueva lógica
        foreach (var payment in payments)
        {
            await UpdatePaymentStatusAsync(payment.Id);
        }

        return true;
    }

    private async Task<(Guid? ContractId, Guid? ReservationId)> ResolveContractAndReservationAsync(
        Guid? contractId,
        Guid? reservationId
    )
    {
        if (contractId.HasValue)
        {
            var contract = await _context.Contracts.FirstOrDefaultAsync(c =>
                c.Id == contractId.Value && c.IsActive
            );
            if (contract == null)
                throw new InvalidOperationException("Contrato no encontrado");

            return (contract.Id, reservationId ?? contract.ReservationId);
        }

        if (reservationId.HasValue)
        {
            var contract = await _context.Contracts.FirstOrDefaultAsync(c =>
                c.ReservationId == reservationId.Value && c.IsActive
            );
            return (contract?.Id, reservationId);
        }

        return (null, null);
    }

    private Task<List<PaymentTransactionPayment>> AutoAssignPaymentsByContractAsync(
        Guid contractId,
        decimal totalAmount,
        bool startFromLast = true
    )
    {
        return AutoAssignPaymentsAsync(
            _context.Payments.Where(p => p.ContractId == contractId && p.IsActive),
            totalAmount,
            startFromLast
        );
    }

    /// <summary>
    /// Asigna automáticamente las cuotas a pagar cuando no se proporcionan PaymentIds específicos.
    /// </summary>
    private Task<List<PaymentTransactionPayment>> AutoAssignPaymentsAsync(
        Guid reservationId,
        decimal totalAmount,
        bool startFromLast = true
    )
    {
        return AutoAssignPaymentsAsync(
            _context.Payments.Where(p => p.ReservationId == reservationId && p.IsActive),
            totalAmount,
            startFromLast
        );
    }

    private async Task<List<PaymentTransactionPayment>> AutoAssignPaymentsAsync(
        IQueryable<Payment> paymentsQuery,
        decimal totalAmount,
        bool startFromLast = true
    )
    {
        var payments = await paymentsQuery
            .OrderBy(p => p.Sequence)
            .ThenBy(p => p.DueDate)
            .ToListAsync();

        var result = new List<PaymentTransactionPayment>();
        decimal remainingAmount = totalAmount;

        if (startFromLast)
        {
            // LÓGICA MAX: Empezar desde la última cuota (fecha más lejana) hacia atrás
            // Esta es la lógica original que ya teníamos implementada
            for (int i = payments.Count - 1; i >= 0 && remainingAmount > 0; i--)
            {
                var payment = payments[i];

                // Calcular cuánto ya se ha pagado por esta cuota
                var alreadyPaid = await _context
                    .PaymentTransactionPayments.Where(ptp => ptp.PaymentId == payment.Id)
                    .SumAsync(ptp => ptp.AmountPaid);

                var remainingForThisPayment = payment.AmountDue - alreadyPaid;

                if (remainingForThisPayment > 0)
                {
                    var amountToPay = Math.Min(remainingAmount, remainingForThisPayment);

                    result.Add(
                        new PaymentTransactionPayment
                        {
                            PaymentId = payment.Id,
                            AmountPaid = amountToPay,
                        }
                    );

                    remainingAmount -= amountToPay;
                }
            }
        }
        else
        {
            // LÓGICA MIN: Empezar desde la primera cuota (fecha más temprana) hacia adelante
            // Esta es la nueva lógica más flexible
            for (int i = 0; i < payments.Count && remainingAmount > 0; i++)
            {
                var payment = payments[i];

                // Calcular cuánto ya se ha pagado por esta cuota
                var alreadyPaid = await _context
                    .PaymentTransactionPayments.Where(ptp => ptp.PaymentId == payment.Id)
                    .SumAsync(ptp => ptp.AmountPaid);

                var remainingForThisPayment = payment.AmountDue - alreadyPaid;

                if (remainingForThisPayment > 0)
                {
                    var amountToPay = Math.Min(remainingAmount, remainingForThisPayment);

                    result.Add(
                        new PaymentTransactionPayment
                        {
                            PaymentId = payment.Id,
                            AmountPaid = amountToPay,
                        }
                    );

                    remainingAmount -= amountToPay;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Actualiza el estado Paid de una cuota basándose en el total pagado.
    /// </summary>
    /// <param name="paymentId">ID de la cuota a actualizar</param>
    private async Task UpdatePaymentStatusAsync(Guid paymentId)
    {
        var totalPaid = await _context
            .PaymentTransactionPayments.Where(ptp => ptp.PaymentId == paymentId)
            .SumAsync(ptp => ptp.AmountPaid);

        var payment = await _context.Payments.FindAsync(paymentId);
        if (payment != null)
        {
            payment.Paid = totalPaid >= payment.AmountDue;
            await _context.SaveChangesAsync();
        }
    }
}
