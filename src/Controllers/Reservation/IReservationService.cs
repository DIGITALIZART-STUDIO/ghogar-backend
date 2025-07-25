using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;

namespace GestionHogar.Services;

public interface IReservationService
{
    Task<IEnumerable<ReservationDto>> GetAllReservationsAsync();
    Task<IEnumerable<ReservationDto>> GetAllCanceledReservationsAsync();
    Task<ReservationDto?> GetReservationByIdAsync(Guid id);
    Task<Reservation> CreateReservationAsync(ReservationCreateDto reservationDto);
    Task<ReservationDto?> UpdateReservationAsync(Guid id, ReservationUpdateDto reservationDto);
    Task<bool> DeleteReservationAsync(Guid id);
    Task<IEnumerable<ReservationDto>> GetReservationsByClientIdAsync(Guid clientId);
    Task<IEnumerable<ReservationDto>> GetReservationsByQuotationIdAsync(Guid quotationId);
    Task<ReservationDto?> ChangeStatusAsync(Guid id, string status);
    Task<byte[]> GenerateReservationPdfAsync(Guid reservationId);
}
