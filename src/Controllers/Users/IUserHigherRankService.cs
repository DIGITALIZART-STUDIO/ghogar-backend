using GestionHogar.Model;

namespace GestionHogar.Controllers;

public interface IUserHigherRankService
{
    /// <summary>
    /// Obtiene todos los usuarios con mayor rango (todos los roles excepto SaleAdvisor) excluyendo al usuario actual
    /// </summary>
    /// <param name="currentUserId">ID del usuario actual que debe ser excluido</param>
    /// <param name="name">Filtro opcional por nombre para autocompletado</param>
    /// <param name="limit">Límite de usuarios a devolver (por defecto 10)</param>
    /// <returns>Lista de usuarios con mayor rango</returns>
    Task<IEnumerable<UserHigherRankDTO>> GetUsersWithHigherRankAsync(
        Guid currentUserId,
        string? name = null,
        int limit = 10
    );

    /// <summary>
    /// Obtiene usuarios con mayor rango paginados
    /// </summary>
    /// <param name="currentUserId">ID del usuario actual que debe ser excluido</param>
    /// <param name="page">Número de página</param>
    /// <param name="pageSize">Tamaño de página</param>
    /// <returns>Respuesta paginada de usuarios con mayor rango</returns>
    Task<PaginatedResponseV2<UserHigherRankDTO>> GetUsersWithHigherRankPaginatedAsync(
        Guid currentUserId,
        int page = 1,
        int pageSize = 10
    );
}
