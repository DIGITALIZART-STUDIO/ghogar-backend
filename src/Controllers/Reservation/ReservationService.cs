using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Services;

public class ReservationService : IReservationService
{
    private readonly DatabaseContext _context;

    public ReservationService(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ReservationDto>> GetAllReservationsAsync()
    {
        return await _context
            .Reservations.Include(r => r.Client)
            .Include(r => r.Quotation)
            .Where(r => r.IsActive)
            .Select(r => new ReservationDto
            {
                Id = r.Id,
                ClientId = r.ClientId,
                ClientName = r.Client.DisplayName,
                QuotationId = r.QuotationId,
                QuotationCode = r.Quotation.Code,
                ReservationDate = r.ReservationDate,
                AmountPaid = r.AmountPaid,
                Currency = r.Currency,
                Status = r.Status,
                PaymentMethod = r.PaymentMethod,
                BankName = r.BankName,
                ExchangeRate = r.ExchangeRate,
                ExpiresAt = r.ExpiresAt,
                Notified = r.Notified,
                Schedule = r.Schedule,
                CreatedAt = r.CreatedAt,
                ModifiedAt = r.ModifiedAt,
            })
            .ToListAsync();
    }

    public async Task<ReservationDto?> GetReservationByIdAsync(Guid id)
    {
        return await _context
            .Reservations.Include(r => r.Client)
            .Include(r => r.Quotation)
            .Where(r => r.Id == id && r.IsActive)
            .Select(r => new ReservationDto
            {
                Id = r.Id,
                ClientId = r.ClientId,
                ClientName = r.Client.DisplayName,
                QuotationId = r.QuotationId,
                QuotationCode = r.Quotation.Code,
                ReservationDate = r.ReservationDate,
                AmountPaid = r.AmountPaid,
                Currency = r.Currency,
                Status = r.Status,
                PaymentMethod = r.PaymentMethod,
                BankName = r.BankName,
                ExchangeRate = r.ExchangeRate,
                ExpiresAt = r.ExpiresAt,
                Notified = r.Notified,
                Schedule = r.Schedule,
                CreatedAt = r.CreatedAt,
                ModifiedAt = r.ModifiedAt,
            })
            .FirstOrDefaultAsync();
    }

    public async Task<Reservation> CreateReservationAsync(ReservationCreateDto reservationDto)
    {
        // Verificar que el cliente existe y está activo
        var client = await _context.Clients.FirstOrDefaultAsync(c =>
            c.Id == reservationDto.ClientId && c.IsActive
        );
        if (client == null)
        {
            throw new ArgumentException("El cliente especificado no existe o está inactivo");
        }

        // Verificar que la cotización existe
        var quotation = await _context.Quotations.FirstOrDefaultAsync(q =>
            q.Id == reservationDto.QuotationId
        );
        if (quotation == null)
        {
            throw new ArgumentException("La cotización especificada no existe");
        }

        // Verificar que no exista ya una reserva activa para esta cotización
        var existingReservation = await _context.Reservations.FirstOrDefaultAsync(r =>
            r.QuotationId == reservationDto.QuotationId && r.IsActive
        );
        if (existingReservation != null)
        {
            throw new ArgumentException("Ya existe una reserva activa para esta cotización");
        }

        var reservation = new Reservation
        {
            ClientId = reservationDto.ClientId,
            QuotationId = reservationDto.QuotationId,
            ReservationDate = reservationDto.ReservationDate,
            AmountPaid = reservationDto.AmountPaid,
            Currency = reservationDto.Currency,
            PaymentMethod = reservationDto.PaymentMethod,
            BankName = reservationDto.BankName,
            ExchangeRate = reservationDto.ExchangeRate,
            ExpiresAt = reservationDto.ExpiresAt,
            Schedule = reservationDto.Schedule,
            Status = ReservationStatus.ISSUED,
            Notified = false,
            Client = client,
            Quotation = quotation,
        };

        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();
        return reservation;
    }

    public async Task<bool> DeleteReservationAsync(Guid id)
    {
        var reservation = await _context.Reservations.FirstOrDefaultAsync(r =>
            r.Id == id && r.IsActive
        );
        if (reservation == null)
            return false;

        // Borrado lógico
        reservation.IsActive = false;
        reservation.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<ReservationDto>> GetReservationsByClientIdAsync(Guid clientId)
    {
        return await _context
            .Reservations.Include(r => r.Client)
            .Include(r => r.Quotation)
            .Where(r => r.ClientId == clientId && r.IsActive)
            .Select(r => new ReservationDto
            {
                Id = r.Id,
                ClientId = r.ClientId,
                ClientName = r.Client.DisplayName,
                QuotationId = r.QuotationId,
                QuotationCode = r.Quotation.Code,
                ReservationDate = r.ReservationDate,
                AmountPaid = r.AmountPaid,
                Currency = r.Currency,
                Status = r.Status,
                PaymentMethod = r.PaymentMethod,
                BankName = r.BankName,
                ExchangeRate = r.ExchangeRate,
                ExpiresAt = r.ExpiresAt,
                Notified = r.Notified,
                Schedule = r.Schedule,
                CreatedAt = r.CreatedAt,
                ModifiedAt = r.ModifiedAt,
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<ReservationDto>> GetReservationsByQuotationIdAsync(
        Guid quotationId
    )
    {
        return await _context
            .Reservations.Include(r => r.Client)
            .Include(r => r.Quotation)
            .Where(r => r.QuotationId == quotationId && r.IsActive)
            .Select(r => new ReservationDto
            {
                Id = r.Id,
                ClientId = r.ClientId,
                ClientName = r.Client.DisplayName,
                QuotationId = r.QuotationId,
                QuotationCode = r.Quotation.Code,
                ReservationDate = r.ReservationDate,
                AmountPaid = r.AmountPaid,
                Currency = r.Currency,
                Status = r.Status,
                PaymentMethod = r.PaymentMethod,
                BankName = r.BankName,
                ExchangeRate = r.ExchangeRate,
                ExpiresAt = r.ExpiresAt,
                Notified = r.Notified,
                Schedule = r.Schedule,
                CreatedAt = r.CreatedAt,
                ModifiedAt = r.ModifiedAt,
            })
            .ToListAsync();
    }
}
