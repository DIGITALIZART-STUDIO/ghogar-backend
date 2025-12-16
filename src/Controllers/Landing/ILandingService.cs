using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;

namespace GestionHogar.Services;

public interface ILandingService
{
    // Método principal para procesar referidos desde la landing
    Task<ReferralResultDto> ProcessReferralFromLandingAsync(ReferralCreateDto referralDto);

    // Método para procesar contacto desde la landing
    Task<ContactResultDto> ProcessContactFromLandingAsync(ContactCreateDto contactDto);

    // Método para obtener proyectos activos
    Task<IEnumerable<Project>> GetActiveProjectsAsync();
}
