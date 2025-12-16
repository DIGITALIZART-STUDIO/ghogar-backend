using GestionHogar.Model;

namespace GestionHogar.Controllers;

public interface IUserHigherRankService
{
    /// <summary>
    /// Obtiene todos los usuarios con mayor rango (todos los roles excepto SaleAdvisor) excluyendo al usuario actual
    /// </summary>
    /// <param name="currentUserId">ID del usuario actual que debe ser excluido</param>
    /// <param name="currentUserRoles">Roles del usuario actual para aplicar filtros específicos</param>
    /// <param name="name">Filtro opcional por nombre para autocompletado</param>
    /// <param name="limit">Límite de usuarios a devolver (por defecto 10)</param>
    /// <returns>Lista de usuarios con mayor rango</returns>
    Task<IEnumerable<UserHigherRankDTO>> GetUsersWithHigherRankAsync(
        Guid currentUserId,
        IList<string> currentUserRoles,
        string? name = null,
        int limit = 10
    );

    /// <summary>
    /// Obtiene usuarios con mayor rango paginados
    /// </summary>
    /// <param name="currentUserId">ID del usuario actual que debe ser excluido</param>
    /// <param name="currentUserRoles">Roles del usuario actual para aplicar filtros específicos</param>
    /// <param name="page">Número de página</param>
    /// <param name="pageSize">Tamaño de página</param>
    /// <param name="search">Término de búsqueda opcional</param>
    /// <param name="orderBy">Campo por el cual ordenar</param>
    /// <param name="orderDirection">Dirección del ordenamiento (asc/desc)</param>
    /// <param name="preselectedId">ID del usuario a preseleccionar en la primera página</param>
    /// <returns>Respuesta paginada de usuarios con mayor rango</returns>
    Task<PaginatedResponseV2<UserHigherRankDTO>> GetUsersWithHigherRankPaginatedAsync(
        Guid currentUserId,
        IList<string> currentUserRoles,
        int page = 1,
        int pageSize = 10,
        string? search = null,
        string? orderBy = null,
        string? orderDirection = "asc",
        string? preselectedId = null
    );
}
