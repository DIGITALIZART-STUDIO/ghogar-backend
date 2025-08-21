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

        query = query.Where(u => u.Roles.Any() && !u.Roles.Contains("SaleAdvisor"));

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
        int page = 1,
        int pageSize = 10
    )
    {
        _logger.LogInformation(
            "Obteniendo usuarios con mayor rango paginados, excluyendo usuario: {CurrentUserId}, página: {Page}, tamaño: {PageSize}",
            currentUserId,
            page,
            pageSize
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
            })
            .Where(u => u.Roles.Any() && !u.Roles.Contains("SaleAdvisor"))
            .OrderByDescending(x => x.Roles.Contains("SUPERADMIN"))
            .ThenByDescending(x => x.Roles.Contains("Admin"))
            .ThenByDescending(x => x.User.CreatedAt)
            .Select(u => new UserHigherRankDTO
            {
                Id = u.User.Id,
                Name = u.User.Name ?? string.Empty,
                Email = u.User.Email ?? string.Empty,
                PhoneNumber = u.User.PhoneNumber ?? string.Empty,
                IsActive = u.User.IsActive,
                CreatedAt = u.User.CreatedAt,
                Roles = u.Roles,
            });

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var users = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var metadata = new PaginationMetadata
        {
            Page = page,
            PageSize = pageSize,
            Total = totalCount,
            TotalPages = totalPages,
            HasPrevious = page > 1,
            HasNext = page < totalPages,
        };

        _logger.LogInformation(
            "Paginación completada: {TotalCount} usuarios, {TotalPages} páginas, página actual: {CurrentPage}",
            totalCount,
            totalPages,
            page
        );

        return new PaginatedResponseV2<UserHigherRankDTO> { Data = users, Meta = metadata };
    }
}
