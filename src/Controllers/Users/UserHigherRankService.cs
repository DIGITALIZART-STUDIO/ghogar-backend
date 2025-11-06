using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Controllers;

public class UserHigherRankService : IUserHigherRankService
{
    private readonly DatabaseContext _db;
    private readonly ILogger<UserHigherRankService> _logger;

    public UserHigherRankService(DatabaseContext db, ILogger<UserHigherRankService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IEnumerable<UserHigherRankDTO>> GetUsersWithHigherRankAsync(
        Guid currentUserId,
        IList<string> currentUserRoles,
        string? name = null,
        int limit = 10
    )
    {
        _logger.LogInformation(
            "Obteniendo usuarios con mayor rango, excluyendo usuario: {CurrentUserId}, filtro nombre: {Name}, límite: {Limit}",
            currentUserId,
            name ?? "sin filtro",
            limit
        );

        // Debug: Verificar usuarios activos
        var totalActiveUsers = await _db.Users.Where(u => u.IsActive).CountAsync();
        _logger.LogInformation(
            "Total usuarios activos en la base de datos: {TotalActiveUsers}",
            totalActiveUsers
        );

        // Debug: Verificar usuarios excluyendo el actual
        var usersExcludingCurrent = await _db
            .Users.Where(u => u.Id != currentUserId && u.IsActive)
            .CountAsync();
        _logger.LogInformation(
            "Usuarios activos excluyendo el actual: {UsersExcludingCurrent}",
            usersExcludingCurrent
        );

        var query = _db
            .Users.Where(u => u.Id != currentUserId && u.IsActive)
            .Select(user => new
            {
                User = user,
                Roles = (
                    from userRole in _db.UserRoles
                    join role in _db.Roles on userRole.RoleId equals role.Id
                    where userRole.UserId == user.Id
                    select role.Name
                ).ToList(),
            });

        // FILTRO ESPECIAL PARA SALESADVISOR: Mostrar todos los roles superiores + solo su supervisor asignado
        if (currentUserRoles.Contains("SalesAdvisor"))
        {
            _logger.LogInformation(
                "Usuario es SalesAdvisor, aplicando filtro para mostrar roles superiores + solo su supervisor asignado"
            );

            // Obtener el ID del supervisor asignado a este SalesAdvisor
            var assignedSupervisorId = await _db
                .SupervisorSalesAdvisors.Where(ssa =>
                    ssa.SalesAdvisorId == currentUserId && ssa.IsActive
                )
                .Select(ssa => ssa.SupervisorId)
                .FirstOrDefaultAsync();

            if (assignedSupervisorId != Guid.Empty)
            {
                _logger.LogInformation(
                    "SalesAdvisor {SalesAdvisorId} tiene supervisor asignado: {SupervisorId}",
                    currentUserId,
                    assignedSupervisorId
                );

                // Filtrar para mostrar:
                // 1. Todos los usuarios con roles superiores (Admin, Manager, etc.) - excluyendo SalesAdvisor
                // 2. Solo su supervisor asignado (incluso si es Supervisor)
                query = query.Where(u =>
                    // Roles superiores (Admin, Manager, etc.) - excluyendo SalesAdvisor
                    (
                        u.Roles.Any()
                        && !u.Roles.Contains("SaleAdvisor")
                        && !u.Roles.Contains("Supervisor")
                    )
                    ||
                    // O su supervisor asignado específico
                    u.User.Id == assignedSupervisorId
                );
            }
            else
            {
                _logger.LogWarning(
                    "SalesAdvisor {SalesAdvisorId} no tiene supervisor asignado, mostrando solo roles superiores (sin supervisores)",
                    currentUserId
                );

                // Si no tiene supervisor asignado, mostrar solo roles superiores (sin supervisores)
                query = query.Where(u =>
                    u.Roles.Any()
                    && !u.Roles.Contains("SaleAdvisor")
                    && !u.Roles.Contains("Supervisor")
                );
            }
        }

        // Debug: Verificar usuarios con roles
        var usersWithRoles = await query.ToListAsync();
        _logger.LogInformation(
            "Usuarios con roles cargados: {UsersWithRolesCount}",
            usersWithRoles.Count
        );

        // Debug: Mostrar cada usuario y sus roles
        foreach (var userWithRoles in usersWithRoles)
        {
            _logger.LogInformation(
                "Usuario: {UserName} (ID: {UserId}), Roles: {Roles}, IsActive: {IsActive}",
                userWithRoles.User.Name,
                userWithRoles.User.Id,
                string.Join(", ", userWithRoles.Roles),
                userWithRoles.User.IsActive
            );
        }

        // Aplicar filtro general de roles (solo si no es SalesAdvisor, ya que SalesAdvisor tiene lógica especial)
        if (!currentUserRoles.Contains("SalesAdvisor"))
        {
            query = query.Where(u => u.Roles.Any() && !u.Roles.Contains("SaleAdvisor"));
        }

        // Debug: Verificar usuarios después del filtro de roles
        var usersAfterRoleFilter = await query.ToListAsync();
        _logger.LogInformation(
            "Usuarios después del filtro de roles (excluyendo SaleAdvisor): {UsersAfterRoleFilterCount}",
            usersAfterRoleFilter.Count
        );

        // Aplicar filtro por nombre si se proporciona
        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(u =>
                u.User.Name != null && u.User.Name.ToLower().Contains(name.ToLower())
            );
        }

        var users = await query
            .OrderByDescending(x => x.Roles.Contains("SUPERADMIN"))
            .ThenByDescending(x => x.Roles.Contains("Admin"))
            .ThenByDescending(x => x.User.CreatedAt)
            .Take(limit) // Límite configurable
            .ToListAsync();

        var result = users.Select(u => new UserHigherRankDTO
        {
            Id = u.User.Id,
            Name = u.User.Name ?? string.Empty,
            Email = u.User.Email ?? string.Empty,
            PhoneNumber = u.User.PhoneNumber ?? string.Empty,
            IsActive = u.User.IsActive,
            CreatedAt = u.User.CreatedAt,
            Roles = u.Roles,
        });

        _logger.LogInformation("Se encontraron {Count} usuarios con mayor rango", result.Count());

        // Debug: Mostrar usuarios finales
        foreach (var user in result)
        {
            _logger.LogInformation(
                "Usuario final: {UserName} (ID: {UserId}), Roles: {Roles}",
                user.Name,
                user.Id,
                string.Join(", ", user.Roles)
            );
        }

        return result;
    }

    public async Task<PaginatedResponseV2<UserHigherRankDTO>> GetUsersWithHigherRankPaginatedAsync(
        Guid currentUserId,
        IList<string> currentUserRoles,
        int page = 1,
        int pageSize = 10,
        string? search = null,
        string? orderBy = null,
        string? orderDirection = "asc",
        string? preselectedId = null
    )
    {
        _logger.LogInformation(
            "Obteniendo usuarios con mayor rango paginados, excluyendo usuario: {CurrentUserId}, página: {Page}, tamaño: {PageSize}, búsqueda: {Search}, preselectedId: {PreselectedId}",
            currentUserId,
            page,
            pageSize,
            search ?? "sin filtro",
            preselectedId ?? "sin preselección"
        );

        var query = _db
            .Users.Where(u => u.Id != currentUserId && u.IsActive)
            .Select(user => new
            {
                User = user,
                Roles = (
                    from userRole in _db.UserRoles
                    join role in _db.Roles on userRole.RoleId equals role.Id
                    where userRole.UserId == user.Id
                    select role.Name
                ).ToList(),
            });

        // FILTRO ESPECIAL PARA SALESADVISOR: Mostrar todos los roles superiores + solo su supervisor asignado
        if (currentUserRoles.Contains("SalesAdvisor"))
        {
            _logger.LogInformation(
                "Usuario es SalesAdvisor, aplicando filtro para mostrar roles superiores + solo su supervisor asignado (paginado)"
            );

            // Obtener el ID del supervisor asignado a este SalesAdvisor
            var assignedSupervisorId = await _db
                .SupervisorSalesAdvisors.Where(ssa =>
                    ssa.SalesAdvisorId == currentUserId && ssa.IsActive
                )
                .Select(ssa => ssa.SupervisorId)
                .FirstOrDefaultAsync();

            if (assignedSupervisorId != Guid.Empty)
            {
                _logger.LogInformation(
                    "SalesAdvisor {SalesAdvisorId} tiene supervisor asignado: {SupervisorId} (paginado)",
                    currentUserId,
                    assignedSupervisorId
                );

                // Filtrar para mostrar:
                // 1. Todos los usuarios con roles superiores (Admin, Manager, etc.) - excluyendo SalesAdvisor
                // 2. Solo su supervisor asignado (incluso si es Supervisor)
                query = query.Where(u =>
                    // Roles superiores (Admin, Manager, etc.) - excluyendo SalesAdvisor
                    (
                        u.Roles.Any()
                        && !u.Roles.Contains("SaleAdvisor")
                        && !u.Roles.Contains("Supervisor")
                    )
                    ||
                    // O su supervisor asignado específico
                    u.User.Id == assignedSupervisorId
                );
            }
            else
            {
                _logger.LogWarning(
                    "SalesAdvisor {SalesAdvisorId} no tiene supervisor asignado, mostrando solo roles superiores (sin supervisores) (paginado)",
                    currentUserId
                );

                // Si no tiene supervisor asignado, mostrar solo roles superiores (sin supervisores)
                query = query.Where(u =>
                    u.Roles.Any()
                    && !u.Roles.Contains("SaleAdvisor")
                    && !u.Roles.Contains("Supervisor")
                );
            }
        }

        // Aplicar filtro general de roles (solo si no es SalesAdvisor, ya que SalesAdvisor tiene lógica especial)
        if (!currentUserRoles.Contains("SalesAdvisor"))
        {
            query = query.Where(u => u.Roles.Any() && !u.Roles.Contains("SaleAdvisor"));
        }

        // Lógica para preselectedId - incluir en la query base
        Guid? preselectedGuid = null;
        if (
            !string.IsNullOrWhiteSpace(preselectedId)
            && Guid.TryParse(preselectedId, out var parsedGuid)
        )
        {
            preselectedGuid = parsedGuid;

            if (page == 1)
            {
                // En la primera página: incluir el usuario preseleccionado al inicio
                var preselectedUser = await _db
                    .Users.Where(u =>
                        u.Id == preselectedGuid && u.Id != currentUserId && u.IsActive
                    )
                    .Select(user => new
                    {
                        User = user,
                        Roles = (
                            from userRole in _db.UserRoles
                            join role in _db.Roles on userRole.RoleId equals role.Id
                            where userRole.UserId == user.Id
                            select role.Name
                        ).ToList(),
                    })
                    .Where(u => u.Roles.Any() && !u.Roles.Contains("SaleAdvisor"))
                    .FirstOrDefaultAsync();

                if (preselectedUser != null)
                {
                    // Modificar la query para que el usuario preseleccionado aparezca primero
                    query = query.OrderBy(u => u.User.Id == preselectedGuid ? 0 : 1);
                }
            }
            else
            {
                // En páginas siguientes: excluir el usuario preseleccionado para evitar duplicados
                query = query.Where(u => u.User.Id != preselectedGuid);
            }
        }

        // Aplicar filtro de búsqueda si se proporciona
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.ToLower();
            query = query.Where(u =>
                (u.User.Name != null && u.User.Name.ToLower().Contains(searchTerm))
                || (u.User.Email != null && u.User.Email.ToLower().Contains(searchTerm))
                || u.Roles.Any(role => role.ToLower().Contains(searchTerm))
            );
        }

        // Aplicar ordenamiento
        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            var isDescending = orderDirection?.ToLower() == "desc";

            // Si hay preselectedId en la primera página, mantenerlo primero
            if (preselectedGuid.HasValue && page == 1)
            {
                query = orderBy.ToLower() switch
                {
                    "name" => isDescending
                        ? query
                            .OrderBy(u => u.User.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(u => u.User.Name)
                        : query
                            .OrderBy(u => u.User.Id == preselectedGuid ? 0 : 1)
                            .ThenBy(u => u.User.Name),
                    "email" => isDescending
                        ? query
                            .OrderBy(u => u.User.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(u => u.User.Email)
                        : query
                            .OrderBy(u => u.User.Id == preselectedGuid ? 0 : 1)
                            .ThenBy(u => u.User.Email),
                    "createdat" => isDescending
                        ? query
                            .OrderBy(u => u.User.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(u => u.User.CreatedAt)
                        : query
                            .OrderBy(u => u.User.Id == preselectedGuid ? 0 : 1)
                            .ThenBy(u => u.User.CreatedAt),
                    _ => query
                        .OrderBy(u => u.User.Id == preselectedGuid ? 0 : 1)
                        .ThenByDescending(u => u.Roles.Contains("SUPERADMIN"))
                        .ThenByDescending(u => u.Roles.Contains("Admin"))
                        .ThenByDescending(u => u.User.CreatedAt),
                };
            }
            else
            {
                query = orderBy.ToLower() switch
                {
                    "name" => isDescending
                        ? query.OrderByDescending(u => u.User.Name)
                        : query.OrderBy(u => u.User.Name),
                    "email" => isDescending
                        ? query.OrderByDescending(u => u.User.Email)
                        : query.OrderBy(u => u.User.Email),
                    "createdat" => isDescending
                        ? query.OrderByDescending(u => u.User.CreatedAt)
                        : query.OrderBy(u => u.User.CreatedAt),
                    _ => query
                        .OrderByDescending(u => u.Roles.Contains("SUPERADMIN"))
                        .ThenByDescending(u => u.Roles.Contains("Admin"))
                        .ThenByDescending(u => u.User.CreatedAt),
                };
            }
        }
        else
        {
            // Ordenamiento por defecto
            if (preselectedGuid.HasValue && page == 1)
            {
                query = query
                    .OrderBy(u => u.User.Id == preselectedGuid ? 0 : 1)
                    .ThenByDescending(u => u.Roles.Contains("SUPERADMIN"))
                    .ThenByDescending(u => u.Roles.Contains("Admin"))
                    .ThenByDescending(u => u.User.CreatedAt);
            }
            else
            {
                query = query
                    .OrderByDescending(u => u.Roles.Contains("SUPERADMIN"))
                    .ThenByDescending(u => u.Roles.Contains("Admin"))
                    .ThenByDescending(u => u.User.CreatedAt);
            }
        }

        // Convertir a DTO
        var dtoQuery = query.Select(u => new UserHigherRankDTO
        {
            Id = u.User.Id,
            Name = u.User.Name ?? string.Empty,
            Email = u.User.Email ?? string.Empty,
            PhoneNumber = u.User.PhoneNumber ?? string.Empty,
            IsActive = u.User.IsActive,
            CreatedAt = u.User.CreatedAt,
            Roles = u.Roles,
        });

        // Ejecutar paginación
        var totalCount = await dtoQuery.CountAsync();
        var users = await dtoQuery.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var metadata = new PaginationMetadata
        {
            Page = page,
            PageSize = pageSize,
            Total = totalCount,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
            HasPrevious = page > 1,
            HasNext = page < (int)Math.Ceiling((double)totalCount / pageSize),
        };

        _logger.LogInformation(
            "Paginación completada: {TotalCount} usuarios, {TotalPages} páginas, página actual: {CurrentPage}",
            totalCount,
            metadata.TotalPages,
            page
        );

        return new PaginatedResponseV2<UserHigherRankDTO> { Data = users, Meta = metadata };
    }
}
