using GestionHogar.Dtos;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Services;

public class PaymentTransactionService(DatabaseContext _context) : IPaymentTransactionService
{
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

    public async Task<IEnumerable<PaymentQuotaSimpleDTO>> GetQuotaStatusByReservationAsync(
        Guid reservationId,
        Guid? excludeTransactionId = null
    )
    {
        var payments = await _context
            .Payments.Include(p => p.Reservation)
            .ThenInclude(r => r.Client)
            .Include(p => p.Reservation)
            .ThenInclude(r => r.Quotation)
            .Where(p => p.ReservationId == reservationId)
            .OrderBy(p => p.DueDate)
            .ToListAsync();

        var transactionsQuery = _context.PaymentTransactions.Where(pt =>
            pt.ReservationId == reservationId
        );

        if (excludeTransactionId.HasValue)
        {
            transactionsQuery = transactionsQuery.Where(pt => pt.Id != excludeTransactionId.Value);
        }

        var transactions = await transactionsQuery.ToListAsync();

        decimal totalPaid = transactions.Sum(t => t.AmountPaid);

        var result = new List<PaymentQuotaSimpleDTO>();

        foreach (var payment in payments)
        {
            decimal paidForThisQuota = Math.Min(totalPaid, payment.AmountDue);

            bool isPaid = paidForThisQuota >= payment.AmountDue;

            // Solo agregar cuotas NO pagadas
            if (!isPaid)
            {
                result.Add(
                    new PaymentQuotaSimpleDTO
                    {
                        Id = payment.Id,
                        ReservationId = payment.ReservationId,
                        ClientName = payment.Reservation.Client.Name!,
                        QuotationCode = payment.Reservation.Quotation.Code,
                        AmountDue = payment.AmountDue,
                        DueDate = payment.DueDate,
                        Paid = false,
                        Currency = payment.Reservation.Currency,
                    }
                );
            }

            totalPaid -= paidForThisQuota;
            if (totalPaid < 0)
                totalPaid = 0;
        }

        return result;
    }

    public async Task<PaymentTransactionDTO> CreateAsync(PaymentTransactionCreateDTO dto)
    {
        var payments = await _context
            .Payments.Where(p => dto.PaymentIds.Contains(p.Id))
            .ToListAsync();

        if (payments.Count != dto.PaymentIds.Count)
            throw new InvalidOperationException("Uno o más pagos no existen");

        var transaction = new PaymentTransaction
        {
            PaymentDate = dto.PaymentDate,
            AmountPaid = dto.AmountPaid,
            ReservationId = dto.ReservationId,
            PaymentMethod = dto.PaymentMethod,
            ReferenceNumber = dto.ReferenceNumber,
            Payments = payments,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
        };

        _context.PaymentTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        // Actualiza el estado Paid de cada cuota
        foreach (var payment in payments)
        {
            // Suma total pagado por esta cuota
            var totalPaidForPayment = await _context
                .PaymentTransactions.Where(pt => pt.Payments.Any(p => p.Id == payment.Id))
                .SumAsync(pt => pt.AmountPaid);

            payment.Paid = totalPaidForPayment >= payment.AmountDue;
        }
        await _context.SaveChangesAsync();

        var created = await _context
            .PaymentTransactions.Include(pt => pt.Payments)
            .FirstAsync(pt => pt.Id == transaction.Id);

        return PaymentTransactionDTO.FromEntity(created);
    }

    public async Task<PaymentTransactionDTO?> UpdateAsync(Guid id, PaymentTransactionUpdateDTO dto)
    {
        var transaction = await _context
            .PaymentTransactions.Include(pt => pt.Payments)
            .FirstOrDefaultAsync(pt => pt.Id == id);

        if (transaction == null)
            return null;

        var payments = await _context
            .Payments.Where(p => dto.PaymentIds.Contains(p.Id))
            .ToListAsync();

        if (payments.Count != dto.PaymentIds.Count)
            throw new InvalidOperationException("Uno o más pagos no existen");

        transaction.PaymentDate = dto.PaymentDate;
        transaction.AmountPaid = dto.AmountPaid;
        transaction.ReservationId = dto.ReservationId;
        transaction.PaymentMethod = dto.PaymentMethod;
        transaction.ReferenceNumber = dto.ReferenceNumber;
        transaction.Payments = payments;
        transaction.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // --- Lógica mejorada ---
        // Actualiza el estado Paid de TODAS las cuotas de la reserva
        var allPayments = await _context
            .Payments.Where(p => p.ReservationId == transaction.ReservationId)
            .ToListAsync();

        foreach (var payment in allPayments)
        {
            var totalPaidForPayment = await _context
                .PaymentTransactions.Where(pt => pt.Payments.Any(p2 => p2.Id == payment.Id))
                .SumAsync(pt => pt.AmountPaid);

            payment.Paid = totalPaidForPayment >= payment.AmountDue;
        }
        await _context.SaveChangesAsync();
        // --- Fin lógica mejorada ---

        var updated = await _context
            .PaymentTransactions.Include(pt => pt.Payments)
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

        _context.PaymentTransactions.Remove(transaction);
        await _context.SaveChangesAsync();

        // Actualiza el estado Paid de cada cuota afectada
        foreach (var payment in payments)
        {
            var totalPaidForPayment = await _context
                .PaymentTransactions.Where(pt => pt.Payments.Any(p => p.Id == payment.Id))
                .SumAsync(pt => pt.AmountPaid);

            payment.Paid = totalPaidForPayment >= payment.AmountDue;
        }
        await _context.SaveChangesAsync();

        return true;
    }
}
