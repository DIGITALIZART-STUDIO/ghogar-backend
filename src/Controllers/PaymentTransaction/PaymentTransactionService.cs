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

    public async Task<PaymentTransactionDTO> CreateAsync(PaymentTransactionCreateDTO dto)
    {
        // Validar pagos
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

        // Recargar con pagos
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

        // Recargar con pagos
        var updated = await _context
            .PaymentTransactions.Include(pt => pt.Payments)
            .FirstAsync(pt => pt.Id == transaction.Id);

        return PaymentTransactionDTO.FromEntity(updated);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var transaction = await _context.PaymentTransactions.FindAsync(id);
        if (transaction == null)
            return false;

        _context.PaymentTransactions.Remove(transaction);
        await _context.SaveChangesAsync();
        return true;
    }
}
