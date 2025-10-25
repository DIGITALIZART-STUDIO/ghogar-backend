using System.Linq.Expressions;
using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GestionHogar.Services;

public class LeadService : ILeadService
{
    private readonly DatabaseContext _context;
    private readonly ILogger<LeadService> _logger;

    public LeadService(DatabaseContext context, ILogger<LeadService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string> GenerateLeadCodeAsync()
    {
        // Formato: LEAD-YYYY-XXXXX donde YYYY es el año actual y XXXXX es un número secuencial
        int year = DateTime.UtcNow.Year;
        var yearPrefix = $"LEAD-{year}-";

        // Buscar el último código generado este año
        var lastLead = await _context
            .Leads.Where(l => l.Code.StartsWith(yearPrefix))
            .OrderByDescending(l => l.Code)
            .FirstOrDefaultAsync();

        int sequence = 1;
        if (lastLead != null)
        {
            var parts = lastLead.Code.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out int lastSequence))
            {
                sequence = lastSequence + 1;
            }
        }

        return $"{yearPrefix}{sequence:D5}";
    }

    public string GenerateClientCodeFromId(Guid clientId)
    {
        // Formato: CLI-XXXXX donde XXXXX es un número derivado del UUID del cliente
        // Usar los últimos 8 caracteres del UUID para crear un código único y consistente
        var uuidString = clientId.ToString("N"); // Sin guiones
        var last8Chars = uuidString.Substring(uuidString.Length - 8);

        // Convertir a número para hacer el código más legible
        var numericValue = Convert.ToInt64(last8Chars, 16);
        var shortCode = (numericValue % 99999).ToString("D5"); // Máximo 5 dígitos

        return $"CLI-{shortCode}";
    }

    public async Task<IEnumerable<Lead>> GetAllLeadsAsync()
    {
        return await _context
            .Leads.OrderByDescending(l => l.CreatedAt)
            .Include(l => l.Client)
            .Include(l => l.AssignedTo)
            .Include(l => l.Project)
            .Include(l => l.Referral)
            .Include(l => l.LastRecycledBy)
            .ToListAsync();
    }

    public async Task<PaginatedResponseV2<Lead>> GetAllLeadsPaginatedAsync(
        int page,
        int pageSize,
        PaginationService paginationService,
        string? search = null,
        LeadStatus[]? status = null,
        LeadCaptureSource[]? captureSource = null,
        LeadCompletionReason[]? completionReason = null,
        Guid? clientId = null,
        Guid? userId = null,
        string? orderBy = null,
        Guid? currentUserId = null,
        IList<string>? currentUserRoles = null,
        bool isSupervisor = false
    )
    {
        // Construir consulta base
        var query = _context.Leads.AsQueryable();

        // FILTRO ESPECIAL PARA SUPERVISORES: Solo mostrar leads de sus SalesAdvisors asignados
        if (isSupervisor && currentUserId.HasValue)
        {
            _logger.LogInformation(
                "Aplicando filtro de supervisor para usuario: {UserId}",
                currentUserId.Value
            );

            // Obtener los IDs de los SalesAdvisors asignados a este supervisor
            var assignedSalesAdvisorIds = await _context
                .SupervisorSalesAdvisors.Where(ssa =>
                    ssa.SupervisorId == currentUserId.Value && ssa.IsActive
                )
                .Select(ssa => ssa.SalesAdvisorId)
                .ToListAsync();

            _logger.LogInformation(
                "Supervisor {SupervisorId} tiene {Count} SalesAdvisors asignados: {SalesAdvisorIds}",
                currentUserId.Value,
                assignedSalesAdvisorIds.Count,
                string.Join(", ", assignedSalesAdvisorIds)
            );

            // Incluir también el propio ID del supervisor para que vea sus propios leads
            assignedSalesAdvisorIds.Add(currentUserId.Value);

            // Filtrar leads de los SalesAdvisors asignados O del propio supervisor
            query = query.Where(l =>
                l.AssignedToId.HasValue && assignedSalesAdvisorIds.Contains(l.AssignedToId.Value)
            );
        }

        // Aplicar filtro de búsqueda si se proporciona
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.ToLower();
            query = query.Where(l =>
                (l.Code != null && l.Code.ToLower().Contains(searchTerm))
                || (
                    l.Client != null
                    && l.Client.Name != null
                    && l.Client.Name.ToLower().Contains(searchTerm)
                )
                || (
                    l.Client != null
                    && l.Client.Email != null
                    && l.Client.Email.ToLower().Contains(searchTerm)
                )
                || (
                    l.Client != null
                    && l.Client.PhoneNumber != null
                    && l.Client.PhoneNumber.Contains(searchTerm)
                )
                || (l.Client != null && l.Client.Dni != null && l.Client.Dni.Contains(searchTerm))
                || (l.Client != null && l.Client.Ruc != null && l.Client.Ruc.Contains(searchTerm))
                || (
                    l.AssignedTo != null
                    && l.AssignedTo.Name != null
                    && l.AssignedTo.Name.ToLower().Contains(searchTerm)
                )
                || (
                    l.Project != null
                    && l.Project.Name != null
                    && l.Project.Name.ToLower().Contains(searchTerm)
                )
            );
        }

        // Aplicar filtro de status si se proporciona
        if (status != null && status.Length > 0)
        {
            query = query.Where(l => status.Contains(l.Status));
        }

        // Aplicar filtro de captureSource si se proporciona
        if (captureSource != null && captureSource.Length > 0)
        {
            query = query.Where(l => captureSource.Contains(l.CaptureSource));
        }

        // Aplicar filtro de completionReason si se proporciona
        if (completionReason != null && completionReason.Length > 0)
        {
            query = query.Where(l =>
                l.CompletionReason.HasValue && completionReason.Contains(l.CompletionReason.Value)
            );
        }

        // Aplicar filtro de clientId si se proporciona
        if (clientId.HasValue)
        {
            query = query.Where(l => l.ClientId == clientId.Value);
        }

        // Aplicar filtro de userId si se proporciona
        if (userId.HasValue)
        {
            query = query.Where(l => l.AssignedToId == userId.Value);
        }

        // Aplicar ordenamiento
        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            var orderParts = orderBy.Split(' ');
            var field = orderParts[0].ToLower();
            var direction =
                orderParts.Length > 1 && orderParts[1].ToLower() == "desc" ? "desc" : "asc";

            query = field switch
            {
                "code" => direction == "desc"
                    ? query.OrderByDescending(l => l.Code)
                    : query.OrderBy(l => l.Code),
                "status" => direction == "desc"
                    ? query.OrderByDescending(l => l.Status)
                    : query.OrderBy(l => l.Status),
                "capturesource" => direction == "desc"
                    ? query.OrderByDescending(l => l.CaptureSource)
                    : query.OrderBy(l => l.CaptureSource),
                "completionreason" => direction == "desc"
                    ? query.OrderByDescending(l => l.CompletionReason)
                    : query.OrderBy(l => l.CompletionReason),
                "entrydate" => direction == "desc"
                    ? query.OrderByDescending(l => l.EntryDate)
                    : query.OrderBy(l => l.EntryDate),
                "expirationdate" => direction == "desc"
                    ? query.OrderByDescending(l => l.ExpirationDate)
                    : query.OrderBy(l => l.ExpirationDate),
                "recyclecount" => direction == "desc"
                    ? query.OrderByDescending(l => l.RecycleCount)
                    : query.OrderBy(l => l.RecycleCount),
                "clientname" => direction == "desc"
                    ? query.OrderByDescending(l => l.Client != null ? l.Client.Name : "")
                    : query.OrderBy(l => l.Client != null ? l.Client.Name : ""),
                "assignedtoname" => direction == "desc"
                    ? query.OrderByDescending(l => l.AssignedTo != null ? l.AssignedTo.Name : "")
                    : query.OrderBy(l => l.AssignedTo != null ? l.AssignedTo.Name : ""),
                "projectname" => direction == "desc"
                    ? query.OrderByDescending(l => l.Project != null ? l.Project.Name : "")
                    : query.OrderBy(l => l.Project != null ? l.Project.Name : ""),
                "isactive" => direction == "desc"
                    ? query.OrderByDescending(l => l.IsActive)
                    : query.OrderBy(l => l.IsActive),
                "createdat" => direction == "desc"
                    ? query.OrderByDescending(l => l.CreatedAt)
                    : query.OrderBy(l => l.CreatedAt),
                "modifiedat" => direction == "desc"
                    ? query.OrderByDescending(l => l.ModifiedAt)
                    : query.OrderBy(l => l.ModifiedAt),
                _ => query.OrderByDescending(l => l.CreatedAt), // Ordenamiento por defecto
            };
        }
        else
        {
            // Ordenamiento por defecto
            query = query.OrderByDescending(l => l.CreatedAt);
        }

        // Incluir las relaciones necesarias
        query = query
            .Include(l => l.Client)
            .Include(l => l.AssignedTo)
            .Include(l => l.Project)
            .Include(l => l.Referral)
            .Include(l => l.LastRecycledBy);

        return await paginationService.PaginateAsync(query, page, pageSize);
    }

    public async Task<Lead?> GetLeadByIdAsync(Guid id)
    {
        return await _context
            .Leads.Include(l => l.Client)
            .Include(l => l.AssignedTo)
            .Include(l => l.Project)
            .Include(l => l.Referral)
            .Include(l => l.LastRecycledBy)
            .FirstOrDefaultAsync(l => l.Id == id && l.IsActive);
    }

    public async Task<Lead> CreateLeadAsync(Lead lead)
    {
        // Generar el código único para el lead
        lead.Code = await GenerateLeadCodeAsync();

        // Asegurarnos de que se establezcan las fechas correctamente
        lead.EntryDate = DateTime.UtcNow;
        lead.ExpirationDate = DateTime.UtcNow.AddDays(7);

        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();
        return lead;
    }

    public async Task<Lead?> UpdateLeadAsync(Guid id, Lead updatedLead)
    {
        var lead = await _context.Leads.FirstOrDefaultAsync(l => l.Id == id && l.IsActive);
        if (lead == null)
            return null;

        lead.ClientId = updatedLead.ClientId;
        lead.AssignedToId = updatedLead.AssignedToId;
        lead.Status = updatedLead.Status;
        lead.CaptureSource = updatedLead.CaptureSource;
        lead.ProjectId = updatedLead.ProjectId;
        lead.CompletionReason = updatedLead.CompletionReason;

        // Si se está cancelando, guardar el motivo
        if (
            updatedLead.Status == LeadStatus.Canceled
            && !string.IsNullOrEmpty(updatedLead.CancellationReason)
        )
        {
            lead.CancellationReason = updatedLead.CancellationReason;
        }

        lead.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return lead;
    }

    public async Task<Lead?> ChangeLeadStatusAsync(
        Guid id,
        LeadStatus status,
        LeadCompletionReason? reason
    )
    {
        var lead = await _context.Leads.FirstOrDefaultAsync(l => l.Id == id && l.IsActive);
        if (lead == null)
            return null;

        lead.Status = status;
        lead.ModifiedAt = DateTime.UtcNow;

        if (status == LeadStatus.Completed || status == LeadStatus.Canceled)
            lead.CompletionReason = reason;
        else
            lead.CompletionReason = null;

        await _context.SaveChangesAsync();
        return lead;
    }

    public async Task<bool> DeleteLeadAsync(Guid id)
    {
        var lead = await _context.Leads.FirstOrDefaultAsync(l => l.Id == id && l.IsActive);
        if (lead == null)
            return false;

        lead.IsActive = false; // El lead queda inactivo
        lead.Status = LeadStatus.Canceled; // Estado cancelado
        lead.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ActivateLeadAsync(Guid id)
    {
        var lead = await _context.Leads.FirstOrDefaultAsync(l => l.Id == id && !l.IsActive);
        if (lead == null)
            return false;

        lead.IsActive = true;

        // Si la fecha de expiración ya pasó, lo ponemos en Expired, si no, en Registered
        if (lead.ExpirationDate < DateTime.UtcNow)
            lead.Status = LeadStatus.Expired;
        else
            lead.Status = LeadStatus.Registered;

        lead.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<Lead>> GetInactiveLeadsAsync()
    {
        return await _context
            .Leads.Where(l => !l.IsActive)
            .Include(l => l.Client)
            .Include(l => l.AssignedTo)
            .Include(l => l.Project)
            .Include(l => l.Referral)
            .Include(l => l.LastRecycledBy)
            .ToListAsync();
    }

    public async Task<IEnumerable<Lead>> GetLeadsByClientIdAsync(Guid clientId)
    {
        return await _context
            .Leads.Where(l => l.IsActive && l.ClientId == clientId)
            .OrderByDescending(l => l.CreatedAt)
            .Include(l => l.AssignedTo)
            .Include(l => l.Project)
            .Include(l => l.Referral)
            .Include(l => l.LastRecycledBy)
            .ToListAsync();
    }

    public async Task<IEnumerable<Lead>> GetLeadsByAssignedToIdAsync(Guid userId)
    {
        return await _context
            .Leads.Where(l => l.IsActive && l.AssignedToId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .Include(l => l.Client)
            .Include(l => l.Project)
            .Include(l => l.Referral)
            .Include(l => l.LastRecycledBy)
            .ToListAsync();
    }

    public async Task<PaginatedResponseV2<Lead>> GetLeadsByAssignedToIdPaginatedAsync(
        Guid userId,
        int page,
        int pageSize,
        PaginationService paginationService,
        string? search = null,
        LeadStatus[]? status = null,
        LeadCaptureSource[]? captureSource = null,
        LeadCompletionReason[]? completionReason = null,
        Guid? clientId = null,
        string? orderBy = null,
        Guid? currentUserId = null,
        IList<string>? currentUserRoles = null,
        bool isSupervisor = false
    )
    {
        // Construir consulta base con filtro de usuario asignado
        var query = _context.Leads.Where(l => l.IsActive && l.AssignedToId == userId);

        // FILTRO ESPECIAL PARA SUPERVISORES: Verificar que el usuario consultado esté asignado al supervisor
        if (isSupervisor && currentUserId.HasValue)
        {
            _logger.LogInformation(
                "Verificando acceso de supervisor {SupervisorId} al usuario {TargetUserId}",
                currentUserId.Value,
                userId
            );

            // Verificar que el usuario consultado esté asignado a este supervisor
            var isUserAssignedToSupervisor = await _context.SupervisorSalesAdvisors.AnyAsync(ssa =>
                ssa.SupervisorId == currentUserId.Value
                && ssa.SalesAdvisorId == userId
                && ssa.IsActive
            );

            if (!isUserAssignedToSupervisor)
            {
                _logger.LogWarning(
                    "Supervisor {SupervisorId} intentó acceder a leads del usuario {TargetUserId} que no está asignado",
                    currentUserId.Value,
                    userId
                );

                // Retornar resultado vacío si el supervisor no tiene acceso a este usuario
                return new PaginatedResponseV2<Lead>
                {
                    Data = new List<Lead>(),
                    Meta = new PaginationMetadata
                    {
                        Page = page,
                        PageSize = pageSize,
                        Total = 0,
                        TotalPages = 0,
                        HasPrevious = false,
                        HasNext = false,
                    },
                };
            }

            _logger.LogInformation(
                "Acceso autorizado: Supervisor {SupervisorId} puede ver leads del usuario {TargetUserId}",
                currentUserId.Value,
                userId
            );
        }

        // Aplicar filtro de búsqueda si se proporciona
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.ToLower();
            query = query.Where(l =>
                (l.Code != null && l.Code.ToLower().Contains(searchTerm))
                || (
                    l.Client != null
                    && l.Client.Name != null
                    && l.Client.Name.ToLower().Contains(searchTerm)
                )
                || (
                    l.Client != null
                    && l.Client.Email != null
                    && l.Client.Email.ToLower().Contains(searchTerm)
                )
                || (
                    l.Client != null
                    && l.Client.PhoneNumber != null
                    && l.Client.PhoneNumber.Contains(searchTerm)
                )
                || (l.Client != null && l.Client.Dni != null && l.Client.Dni.Contains(searchTerm))
                || (l.Client != null && l.Client.Ruc != null && l.Client.Ruc.Contains(searchTerm))
                || (
                    l.AssignedTo != null
                    && l.AssignedTo.Name != null
                    && l.AssignedTo.Name.ToLower().Contains(searchTerm)
                )
                || (
                    l.Project != null
                    && l.Project.Name != null
                    && l.Project.Name.ToLower().Contains(searchTerm)
                )
            );
        }

        // Aplicar filtro de status si se proporciona
        if (status != null && status.Length > 0)
        {
            query = query.Where(l => status.Contains(l.Status));
        }

        // Aplicar filtro de captureSource si se proporciona
        if (captureSource != null && captureSource.Length > 0)
        {
            query = query.Where(l => captureSource.Contains(l.CaptureSource));
        }

        // Aplicar filtro de completionReason si se proporciona
        if (completionReason != null && completionReason.Length > 0)
        {
            query = query.Where(l =>
                l.CompletionReason.HasValue && completionReason.Contains(l.CompletionReason.Value)
            );
        }

        // Aplicar filtro de clientId si se proporciona
        if (clientId.HasValue)
        {
            query = query.Where(l => l.ClientId == clientId.Value);
        }

        // Aplicar ordenamiento
        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            var orderParts = orderBy.Split(' ');
            var field = orderParts[0].ToLower();
            var direction =
                orderParts.Length > 1 && orderParts[1].ToLower() == "desc" ? "desc" : "asc";

            query = field switch
            {
                "code" => direction == "desc"
                    ? query.OrderByDescending(l => l.Code)
                    : query.OrderBy(l => l.Code),
                "status" => direction == "desc"
                    ? query.OrderByDescending(l => l.Status)
                    : query.OrderBy(l => l.Status),
                "capturesource" => direction == "desc"
                    ? query.OrderByDescending(l => l.CaptureSource)
                    : query.OrderBy(l => l.CaptureSource),
                "completionreason" => direction == "desc"
                    ? query.OrderByDescending(l => l.CompletionReason)
                    : query.OrderBy(l => l.CompletionReason),
                "entrydate" => direction == "desc"
                    ? query.OrderByDescending(l => l.EntryDate)
                    : query.OrderBy(l => l.EntryDate),
                "expirationdate" => direction == "desc"
                    ? query.OrderByDescending(l => l.ExpirationDate)
                    : query.OrderBy(l => l.ExpirationDate),
                "recyclecount" => direction == "desc"
                    ? query.OrderByDescending(l => l.RecycleCount)
                    : query.OrderBy(l => l.RecycleCount),
                "clientname" => direction == "desc"
                    ? query.OrderByDescending(l => l.Client != null ? l.Client.Name : "")
                    : query.OrderBy(l => l.Client != null ? l.Client.Name : ""),
                "assignedtoname" => direction == "desc"
                    ? query.OrderByDescending(l => l.AssignedTo != null ? l.AssignedTo.Name : "")
                    : query.OrderBy(l => l.AssignedTo != null ? l.AssignedTo.Name : ""),
                "projectname" => direction == "desc"
                    ? query.OrderByDescending(l => l.Project != null ? l.Project.Name : "")
                    : query.OrderBy(l => l.Project != null ? l.Project.Name : ""),
                "isactive" => direction == "desc"
                    ? query.OrderByDescending(l => l.IsActive)
                    : query.OrderBy(l => l.IsActive),
                "createdat" => direction == "desc"
                    ? query.OrderByDescending(l => l.CreatedAt)
                    : query.OrderBy(l => l.CreatedAt),
                "modifiedat" => direction == "desc"
                    ? query.OrderByDescending(l => l.ModifiedAt)
                    : query.OrderBy(l => l.ModifiedAt),
                _ => query.OrderByDescending(l => l.CreatedAt), // Ordenamiento por defecto
            };
        }
        else
        {
            // Ordenamiento por defecto
            query = query.OrderByDescending(l => l.CreatedAt);
        }

        // Incluir las relaciones necesarias
        query = query
            .Include(l => l.Client)
            .Include(l => l.Project)
            .Include(l => l.Referral)
            .Include(l => l.LastRecycledBy);

        return await paginationService.PaginateAsync(query, page, pageSize);
    }

    public async Task<IEnumerable<Lead>> GetLeadsByStatusAsync(LeadStatus status)
    {
        return await _context
            .Leads.Where(l => l.IsActive && l.Status == status)
            .Include(l => l.Client)
            .Include(l => l.AssignedTo)
            .Include(l => l.Project)
            .Include(l => l.Referral)
            .Include(l => l.LastRecycledBy)
            .ToListAsync();
    }

    public async Task<IEnumerable<UserSummaryDto>> GetUsersSummaryAsync()
    {
        return await _context
            .Users.Where(u => u.IsActive)
            .Select(u => new UserSummaryDto
            {
                Id = u.Id,
                UserName = u.Name,
                Email = u.Email,
                Roles = (
                    from userRole in _context.UserRoles
                    join role in _context.Roles on userRole.RoleId equals role.Id
                    where userRole.UserId == u.Id
                    select role.Name
                ).ToList(),
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<UserSummaryDto>> GetUsersWithLeadsSummaryAsync(
        Guid? projectId = null,
        Guid? currentUserId = null,
        IList<string>? currentUserRoles = null,
        bool isSupervisor = false
    )
    {
        var query = _context
            .Users.Where(u => u.IsActive)
            .Where(u => _context.Leads.Any(l => l.AssignedToId == u.Id))
            .AsQueryable();

        // FILTRO ESPECIAL PARA SUPERVISORES: Solo mostrar usuarios asignados al supervisor
        if (isSupervisor && currentUserId.HasValue)
        {
            _logger.LogInformation(
                "Aplicando filtro de supervisor en GetUsersWithLeadsSummaryAsync para usuario: {UserId}",
                currentUserId.Value
            );

            // Obtener los IDs de los SalesAdvisors asignados a este supervisor
            var assignedSalesAdvisorIds = await _context
                .SupervisorSalesAdvisors.Where(ssa =>
                    ssa.SupervisorId == currentUserId.Value && ssa.IsActive
                )
                .Select(ssa => ssa.SalesAdvisorId)
                .ToListAsync();

            _logger.LogInformation(
                "Supervisor {SupervisorId} tiene {Count} SalesAdvisors asignados en summary",
                currentUserId.Value,
                assignedSalesAdvisorIds.Count
            );

            // Filtrar usuarios que están asignados a este supervisor
            query = query.Where(u => assignedSalesAdvisorIds.Contains(u.Id));
        }

        // Aplicar filtro por proyecto si se especifica
        if (projectId.HasValue)
        {
            query = query.Where(u =>
                _context.Leads.Any(l => l.AssignedToId == u.Id && l.ProjectId == projectId.Value)
            );
        }

        return await query
            .Select(u => new UserSummaryDto
            {
                Id = u.Id,
                UserName = u.Name,
                Email = u.Email,
                Roles = (
                    from userRole in _context.UserRoles
                    join role in _context.Roles on userRole.RoleId equals role.Id
                    where userRole.UserId == u.Id
                    select role.Name
                ).ToList(),
            })
            .ToListAsync();
    }

    public async Task<PaginatedResponseV2<UserSummaryDto>> GetUsersSummaryPaginatedAsync(
        int page,
        int pageSize,
        string? search = null,
        string? orderBy = null,
        string? orderDirection = "asc",
        string? preselectedId = null,
        Guid? currentUserId = null,
        IList<string>? currentUserRoles = null
    )
    {
        // Diccionario para traducción de roles de español a inglés
        var roleTranslation = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Super Administrador", "SuperAdmin" },
            { "Administrador", "Admin" },
            { "Supervisor", "Supervisor" },
            { "Asesor de Ventas", "SalesAdvisor" },
            { "Gerente", "Manager" },
            { "Gerente de Finanzas", "FinanceManager" },
        };

        var query = _context
            .Users.Where(u => u.IsActive)
            .Select(u => new UserSummaryDto
            {
                Id = u.Id,
                UserName = u.Name,
                Email = u.Email,
                Roles = (
                    from userRole in _context.UserRoles
                    join role in _context.Roles on userRole.RoleId equals role.Id
                    where userRole.UserId == u.Id
                    select role.Name
                ).ToList(),
            });

        // FILTRO ESPECIAL PARA SALESADVISOR Y SUPERVISOR
        if (currentUserRoles != null && currentUserId.HasValue)
        {
            if (currentUserRoles.Contains("SalesAdvisor"))
            {
                _logger.LogInformation(
                    "Usuario es SalesAdvisor, aplicando filtro para mostrar solo a sí mismo: {UserId}",
                    currentUserId.Value
                );

                // SalesAdvisor solo ve a sí mismo
                query = query.Where(u => u.Id == currentUserId.Value);
            }
            else if (currentUserRoles.Contains("Supervisor"))
            {
                _logger.LogInformation(
                    "Usuario es Supervisor, aplicando filtro para mostrar a sí mismo y sus SalesAdvisors asignados: {UserId}",
                    currentUserId.Value
                );

                // Obtener los IDs de los SalesAdvisors asignados a este supervisor
                var assignedSalesAdvisorIds = await _context
                    .SupervisorSalesAdvisors.Where(ssa =>
                        ssa.SupervisorId == currentUserId.Value && ssa.IsActive
                    )
                    .Select(ssa => ssa.SalesAdvisorId)
                    .ToListAsync();

                // Incluir también el propio ID del supervisor
                assignedSalesAdvisorIds.Add(currentUserId.Value);

                _logger.LogInformation(
                    "Supervisor {SupervisorId} tiene {Count} SalesAdvisors asignados: {SalesAdvisorIds}",
                    currentUserId.Value,
                    assignedSalesAdvisorIds.Count,
                    string.Join(", ", assignedSalesAdvisorIds)
                );

                // Filtrar usuarios que están asignados a este supervisor + el supervisor mismo
                query = query.Where(u => assignedSalesAdvisorIds.Contains(u.Id));
            }
            // Para otros roles (Admin, Manager, etc.) no se aplica filtro - ven todos los usuarios
        }

        // Lógica para preselectedId - incluir en la query base
        Guid? preselectedLeadGuid = null;
        if (
            !string.IsNullOrWhiteSpace(preselectedId)
            && Guid.TryParse(preselectedId, out var parsedGuid)
        )
        {
            preselectedLeadGuid = parsedGuid;

            if (page == 1)
            {
                // En la primera página: incluir el usuario preseleccionado al inicio
                var preselectedUser = await _context.Users.FirstOrDefaultAsync(u =>
                    u.Id == preselectedLeadGuid && u.IsActive
                );

                if (preselectedUser != null)
                {
                    // Modificar la query para que el usuario preseleccionado aparezca primero
                    query = query.OrderBy(u => u.Id == preselectedLeadGuid ? 0 : 1);
                }
            }
            else
            {
                // En páginas siguientes: excluir el usuario preseleccionado para evitar duplicados
                query = query.Where(u => u.Id != preselectedLeadGuid);
            }
        }

        // Aplicar filtro de búsqueda si se proporciona
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.ToLower();

            // Función para encontrar traducciones inteligentes
            var translatedTerms = GetIntelligentRoleTranslations(searchTerm, roleTranslation);

            // Aplicar filtros de búsqueda
            query = query.Where(u =>
                (u.UserName != null && u.UserName.ToLower().Contains(searchTerm))
                || (u.Email != null && u.Email.ToLower().Contains(searchTerm))
                || u.Roles.Any(role => role.ToLower().Contains(searchTerm))
                || u.Roles.Any(role => translatedTerms.Any(term => role.ToLower().Contains(term)))
            );
        }

        // Aplicar ordenamiento
        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            var isDescending = orderDirection?.ToLower() == "desc";

            // Si hay preselectedId en la primera página, mantenerlo primero
            if (preselectedLeadGuid.HasValue && page == 1)
            {
                query = orderBy.ToLower() switch
                {
                    "name" => isDescending
                        ? query
                            .OrderBy(u => u.Id == preselectedLeadGuid ? 0 : 1)
                            .ThenByDescending(u => u.UserName)
                        : query
                            .OrderBy(u => u.Id == preselectedLeadGuid ? 0 : 1)
                            .ThenBy(u => u.UserName),
                    "email" => isDescending
                        ? query
                            .OrderBy(u => u.Id == preselectedLeadGuid ? 0 : 1)
                            .ThenByDescending(u => u.Email)
                        : query
                            .OrderBy(u => u.Id == preselectedLeadGuid ? 0 : 1)
                            .ThenBy(u => u.Email),
                    "roles" => isDescending
                        ? query
                            .OrderBy(u => u.Id == preselectedLeadGuid ? 0 : 1)
                            .ThenByDescending(u => u.Roles.FirstOrDefault())
                        : query
                            .OrderBy(u => u.Id == preselectedLeadGuid ? 0 : 1)
                            .ThenBy(u => u.Roles.FirstOrDefault()),
                    _ => query
                        .OrderBy(u => u.Id == preselectedLeadGuid ? 0 : 1)
                        .ThenBy(u => u.UserName),
                };
            }
            else
            {
                query = orderBy.ToLower() switch
                {
                    "name" => isDescending
                        ? query.OrderByDescending(u => u.UserName)
                        : query.OrderBy(u => u.UserName),
                    "email" => isDescending
                        ? query.OrderByDescending(u => u.Email)
                        : query.OrderBy(u => u.Email),
                    "roles" => isDescending
                        ? query.OrderByDescending(u => u.Roles.FirstOrDefault())
                        : query.OrderBy(u => u.Roles.FirstOrDefault()),
                    _ => query.OrderBy(u => u.UserName), // Ordenamiento por defecto
                };
            }
        }
        else
        {
            // Ordenamiento por defecto
            if (preselectedLeadGuid.HasValue && page == 1)
            {
                query = query
                    .OrderBy(u => u.Id == preselectedLeadGuid ? 0 : 1)
                    .ThenBy(u => u.UserName);
            }
            else
            {
                query = query.OrderBy(u => u.UserName);
            }
        }

        // Ejecutar paginación
        var totalCount = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return PaginatedResponseV2<UserSummaryDto>.Create(items, totalCount, page, pageSize);
    }

    /**
     * Método para obtener un resumen de los leads asignados a un usuario.
     * Incluye información del cliente y proyecto.
     */
    public async Task<IEnumerable<LeadSummaryDto>> GetAssignedLeadsSummaryAsync(Guid assignedToId)
    {
        var leads = await _context
            .Leads.Where(l => l.IsActive && l.AssignedToId == assignedToId)
            .Include(l => l.Client)
            .Include(l => l.Project)
            .Include(l => l.Referral)
            .Include(l => l.LastRecycledBy)
            .ToListAsync();

        return leads.Select(LeadSummaryDto.FromEntity);
    }

    /**
     * Método para obtener clientes disponibles para cotización (solo para roles mayores a SalesAdvisor).
     * Muestra todos los clientes excepto los que ya tienen leads asignados.
     */
    public async Task<IEnumerable<LeadSummaryDto>> GetAvailableLeadsForQuotationByUserAsync(
        Guid currentUserId,
        Guid? excludeQuotationId = null,
        IList<string>? currentUserRoles = null
    )
    {
        // Verificar si el usuario tiene roles mayores a SalesAdvisor
        var hasHigherRole =
            currentUserRoles?.Any(role =>
                role != "SalesAdvisor"
                && (
                    role == "SuperAdmin"
                    || role == "Admin"
                    || role == "Supervisor"
                    || role == "Manager"
                    || role == "FinanceManager"
                )
            ) ?? false;

        // Obtener leads que ya tienen cotizaciones activas (para excluirlos)
        var leadsWithActiveQuotations = await _context
            .Quotations.Where(q => q.Status != QuotationStatus.CANCELED)
            .Select(q => q.LeadId)
            .ToListAsync();

        // Si se proporciona excludeQuotationId, excluir también ese lead específico
        if (excludeQuotationId.HasValue)
        {
            var excludedLeadId = await _context
                .Quotations.Where(q => q.Id == excludeQuotationId.Value)
                .Select(q => q.LeadId)
                .FirstOrDefaultAsync();

            if (excludedLeadId != Guid.Empty)
            {
                leadsWithActiveQuotations.Remove(excludedLeadId);
            }
        }

        if (hasHigherRole)
        {
            // Para roles mayores a SalesAdvisor: mostrar leads asignados + clientes sin leads
            var allClients = await _context.Clients.Where(c => c.IsActive).ToListAsync();

            // Obtener todos los leads del usuario actual con sus clientes incluidos
            // EXCLUIR leads que ya tienen cotizaciones activas
            var userLeads = await _context
                .Leads.Where(l =>
                    l.AssignedToId == currentUserId
                    && l.IsActive
                    && l.Status != LeadStatus.Canceled
                    && l.Status != LeadStatus.Expired
                    && l.Status != LeadStatus.Completed
                    && !leadsWithActiveQuotations.Contains(l.Id) // Excluir leads con cotizaciones activas
                )
                .Include(l => l.Client) // Incluir explícitamente el cliente
                .Include(l => l.Project)
                .Include(l => l.Referral)
                .Include(l => l.LastRecycledBy)
                .ToListAsync();

            // Crear un set de clientes que ya tienen leads asignados al usuario
            var clientsWithUserLeads = userLeads.Select(l => l.ClientId).ToHashSet();

            var result = new List<LeadSummaryDto>();

            // Agregar los leads reales del usuario
            foreach (var lead in userLeads)
            {
                result.Add(LeadSummaryDto.FromEntity(lead));
            }

            // Agregar clientes que NO tienen leads asignados al usuario actual
            foreach (var client in allClients)
            {
                if (!clientsWithUserLeads.Contains(client.Id))
                {
                    // Crear un LeadSummaryDto virtual para este cliente
                    var virtualLead = new LeadSummaryDto
                    {
                        Id = client.Id, // ID temporal para el lead virtual
                        Code = GenerateClientCodeFromId(client.Id), // Código único y consistente basado en el UUID del cliente
                        Client = new ClientSummaryDto
                        {
                            Id = client.Id,
                            Name = client.Name ?? string.Empty,
                            Dni = client.Dni,
                            Ruc = client.Ruc,
                            PhoneNumber = client.PhoneNumber,
                        },
                        Status = LeadStatus.Registered, // Estado por defecto
                        ExpirationDate = DateTime.UtcNow.AddDays(7), // Fecha de expiración por defecto
                        ProjectName = null, // Sin proyecto asignado
                        RecycleCount = 0, // Sin reciclajes
                    };

                    result.Add(virtualLead);
                }
            }

            return result;
        }
        else
        {
            // Para SalesAdvisor: mostrar solo sus leads asignados
            // EXCLUIR leads que ya tienen cotizaciones activas
            var userLeads = await _context
                .Leads.Where(l =>
                    l.AssignedToId == currentUserId
                    && l.IsActive
                    && l.Status != LeadStatus.Canceled
                    && l.Status != LeadStatus.Expired
                    && l.Status != LeadStatus.Completed
                    && !leadsWithActiveQuotations.Contains(l.Id) // Excluir leads con cotizaciones activas
                )
                .Include(l => l.Client) // Incluir explícitamente el cliente
                .Include(l => l.Project)
                .Include(l => l.Referral)
                .Include(l => l.LastRecycledBy)
                .ToListAsync();

            var result = new List<LeadSummaryDto>();

            // Agregar solo los leads reales del usuario
            foreach (var lead in userLeads)
            {
                result.Add(LeadSummaryDto.FromEntity(lead));
            }

            return result;
        }
    }

    public async Task<
        PaginatedResponseV2<LeadSummaryDto>
    > GetAvailableLeadsForQuotationPaginatedAsync(
        Guid currentUserId,
        IList<string> currentUserRoles,
        int page,
        int pageSize,
        string? search = null,
        string? orderBy = null,
        string? orderDirection = "asc",
        string? preselectedId = null
    )
    {
        _logger.LogInformation(
            "Obteniendo leads disponibles para cotización paginados para usuario: {UserId} con roles: {Roles}, página: {Page}, tamaño: {PageSize}, búsqueda: {Search}, preselectedId: {PreselectedId}",
            currentUserId,
            string.Join(", ", currentUserRoles),
            page,
            pageSize,
            search ?? "null",
            preselectedId ?? "null"
        );

        // Lógica para preselectedId - incluir en la query base
        Guid? preselectedLeadGuid = null;
        Guid? excludeQuotationId = null;

        if (!string.IsNullOrWhiteSpace(preselectedId))
        {
            if (Guid.TryParse(preselectedId, out var parsedGuid))
            {
                // Verificar si es un ID de cotización
                var quotationExists = await _context.Quotations.AnyAsync(q => q.Id == parsedGuid);

                if (quotationExists)
                {
                    // Es un ID de cotización - obtener el lead asociado
                    var leadId = await _context
                        .Quotations.Where(q => q.Id == parsedGuid)
                        .Select(q => q.LeadId)
                        .FirstOrDefaultAsync();

                    if (leadId != Guid.Empty)
                    {
                        preselectedLeadGuid = leadId;
                        excludeQuotationId = parsedGuid;
                    }
                }
                else
                {
                    // Es un ID de lead directamente
                    preselectedLeadGuid = parsedGuid;
                }
            }
        }

        // Verificar si el usuario tiene roles mayores a SalesAdvisor
        var hasHigherRole = currentUserRoles.Any(role =>
            role != "SalesAdvisor"
            && (
                role == "SuperAdmin"
                || role == "Admin"
                || role == "Supervisor"
                || role == "Manager"
                || role == "FinanceManager"
            )
        );

        // Obtener leads que ya tienen cotizaciones activas (para excluirlos)
        var leadsWithActiveQuotations = await _context
            .Quotations.Where(q => q.Status != QuotationStatus.CANCELED)
            .Select(q => q.LeadId)
            .ToListAsync();

        // Si se proporciona excludeQuotationId, excluir también ese lead específico
        if (excludeQuotationId.HasValue)
        {
            var excludedLeadId = await _context
                .Quotations.Where(q => q.Id == excludeQuotationId.Value)
                .Select(q => q.LeadId)
                .FirstOrDefaultAsync();

            if (excludedLeadId != Guid.Empty)
            {
                leadsWithActiveQuotations.Remove(excludedLeadId);
            }
        }

        // Construir la query base directamente (como en GetUsersSummaryPaginatedAsync)
        IQueryable<LeadSummaryDto> query;

        if (hasHigherRole)
        {
            // Para roles mayores a SalesAdvisor: mostrar leads asignados + clientes sin leads
            var allClients = await _context.Clients.Where(c => c.IsActive).ToListAsync();

            // Obtener todos los leads del usuario actual
            var userLeads = await _context
                .Leads.Where(l =>
                    l.AssignedToId == currentUserId
                    && l.IsActive
                    && l.Status != LeadStatus.Canceled
                    && l.Status != LeadStatus.Expired
                    && l.Status != LeadStatus.Completed
                    && (
                        !leadsWithActiveQuotations.Contains(l.Id)
                        || (preselectedLeadGuid.HasValue && l.Id == preselectedLeadGuid.Value)
                    )
                )
                .Include(l => l.Client)
                .Include(l => l.Project)
                .Include(l => l.Referral)
                .Include(l => l.LastRecycledBy)
                .ToListAsync();

            // Crear un set de clientes que ya tienen leads asignados al usuario
            var clientsWithUserLeads = userLeads.Select(l => l.ClientId).ToHashSet();

            var result = new List<LeadSummaryDto>();

            // Agregar los leads reales del usuario
            foreach (var lead in userLeads)
            {
                result.Add(LeadSummaryDto.FromEntity(lead));
            }

            // Agregar clientes que NO tienen leads asignados al usuario actual
            foreach (var client in allClients)
            {
                if (!clientsWithUserLeads.Contains(client.Id))
                {
                    // Crear un LeadSummaryDto virtual para este cliente
                    var virtualLead = new LeadSummaryDto
                    {
                        Id = client.Id,
                        Code = GenerateClientCodeFromId(client.Id),
                        Client = new ClientSummaryDto
                        {
                            Id = client.Id,
                            Name = client.Name ?? string.Empty,
                            Dni = client.Dni,
                            Ruc = client.Ruc,
                            PhoneNumber = client.PhoneNumber,
                        },
                        Status = LeadStatus.Registered,
                        ExpirationDate = DateTime.UtcNow.AddDays(7),
                        ProjectName = null,
                        RecycleCount = 0,
                    };

                    result.Add(virtualLead);
                }
            }

            query = result.AsQueryable();
        }
        else
        {
            // Para SalesAdvisor: mostrar solo sus leads asignados
            var userLeads = await _context
                .Leads.Where(l =>
                    l.AssignedToId == currentUserId
                    && l.IsActive
                    && l.Status != LeadStatus.Canceled
                    && l.Status != LeadStatus.Expired
                    && l.Status != LeadStatus.Completed
                    && (
                        !leadsWithActiveQuotations.Contains(l.Id)
                        || (preselectedLeadGuid.HasValue && l.Id == preselectedLeadGuid.Value)
                    )
                )
                .Include(l => l.Client)
                .Include(l => l.Project)
                .Include(l => l.Referral)
                .Include(l => l.LastRecycledBy)
                .ToListAsync();

            var result = new List<LeadSummaryDto>();

            // Agregar solo los leads reales del usuario
            foreach (var lead in userLeads)
            {
                result.Add(LeadSummaryDto.FromEntity(lead));
            }

            query = result.AsQueryable();
        }

        // Aplicar filtro de búsqueda si se proporciona
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(l =>
                (l.Code != null && l.Code.ToLower().Contains(searchLower))
                || (
                    l.Client != null
                    && l.Client.Name != null
                    && l.Client.Name.ToLower().Contains(searchLower)
                )
                || (
                    l.Client != null
                    && l.Client.Dni != null
                    && l.Client.Dni.ToLower().Contains(searchLower)
                )
                || (
                    l.Client != null
                    && l.Client.Ruc != null
                    && l.Client.Ruc.ToLower().Contains(searchLower)
                )
                || (
                    l.Client != null
                    && l.Client.PhoneNumber != null
                    && l.Client.PhoneNumber.ToLower().Contains(searchLower)
                )
                || (l.ProjectName != null && l.ProjectName.ToLower().Contains(searchLower))
            );
        }

        // Aplicar ordenamiento
        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            var isDescending = orderDirection?.ToLower() == "desc";

            // Si hay preselectedId en la primera página, mantenerlo primero
            if (preselectedLeadGuid.HasValue && page == 1)
            {
                query = orderBy.ToLower() switch
                {
                    "code" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedLeadGuid ? 0 : 1)
                            .ThenByDescending(l => l.Code)
                        : query
                            .OrderBy(l => l.Id == preselectedLeadGuid ? 0 : 1)
                            .ThenBy(l => l.Code),
                    "clientname" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedLeadGuid ? 0 : 1)
                            .ThenByDescending(l => l.Client != null ? l.Client.Name : "")
                        : query
                            .OrderBy(l => l.Id == preselectedLeadGuid ? 0 : 1)
                            .ThenBy(l => l.Client != null ? l.Client.Name : ""),
                    "status" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedLeadGuid ? 0 : 1)
                            .ThenByDescending(l => l.Status)
                        : query
                            .OrderBy(l => l.Id == preselectedLeadGuid ? 0 : 1)
                            .ThenBy(l => l.Status),
                    "expirationdate" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedLeadGuid ? 0 : 1)
                            .ThenByDescending(l => l.ExpirationDate)
                        : query
                            .OrderBy(l => l.Id == preselectedLeadGuid ? 0 : 1)
                            .ThenBy(l => l.ExpirationDate),
                    _ => query
                        .OrderBy(l => l.Id == preselectedLeadGuid ? 0 : 1)
                        .ThenByDescending(l => l.ExpirationDate),
                };
            }
            else
            {
                query = orderBy.ToLower() switch
                {
                    "code" => isDescending
                        ? query.OrderByDescending(l => l.Code)
                        : query.OrderBy(l => l.Code),
                    "clientname" => isDescending
                        ? query.OrderByDescending(l => l.Client != null ? l.Client.Name : "")
                        : query.OrderBy(l => l.Client != null ? l.Client.Name : ""),
                    "status" => isDescending
                        ? query.OrderByDescending(l => l.Status)
                        : query.OrderBy(l => l.Status),
                    "expirationdate" => isDescending
                        ? query.OrderByDescending(l => l.ExpirationDate)
                        : query.OrderBy(l => l.ExpirationDate),
                    _ => query.OrderByDescending(l => l.ExpirationDate), // Ordenamiento por defecto
                };
            }
        }
        else
        {
            // Ordenamiento por defecto
            if (preselectedLeadGuid.HasValue && page == 1)
            {
                query = query
                    .OrderBy(l => l.Id == preselectedLeadGuid ? 0 : 1)
                    .ThenByDescending(l => l.ExpirationDate);
            }
            else
            {
                query = query.OrderByDescending(l => l.ExpirationDate);
            }
        }

        // Aplicar lógica de desplazamiento del preselectedId (como en GetUsersSummaryPaginatedAsync)
        if (preselectedLeadGuid.HasValue && page == 1)
        {
            // Verificar que el lead preseleccionado existe en los resultados
            var preselectedLead = query.FirstOrDefault(l => l.Id == preselectedLeadGuid);
            if (preselectedLead != null)
            {
                // Reordenar para que el lead preseleccionado aparezca primero
                query = query.OrderBy(l => l.Id == preselectedLeadGuid ? 0 : 1);
            }
        }

        // Aplicar lógica de exclusión del preselectedId para páginas 2+
        if (preselectedLeadGuid.HasValue && page > 1)
        {
            query = query.Where(l => l.Id != preselectedLeadGuid.Value);
        }

        // Aplicar paginación
        var totalCount = query.Count();
        var leads = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        // Crear metadatos de paginación
        var paginationMetadata = new PaginationMetadata
        {
            Page = page,
            PageSize = pageSize,
            Total = totalCount,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
            HasPrevious = page > 1,
            HasNext = page < (int)Math.Ceiling((double)totalCount / pageSize),
        };

        return new PaginatedResponseV2<LeadSummaryDto> { Data = leads, Meta = paginationMetadata };
    }

    // Nuevos métodos para manejar reciclaje y expiración

    public async Task<Lead?> RecycleLeadAsync(Guid id, Guid userId)
    {
        var lead = await _context.Leads.FirstOrDefaultAsync(l =>
            l.Id == id
            && l.IsActive
            && (l.Status == LeadStatus.Expired || l.Status == LeadStatus.Canceled)
        );

        if (lead == null)
            return null;

        lead.RecycleLead(userId);

        await _context.SaveChangesAsync();

        return lead;
    }

    public async Task<IEnumerable<Lead>> GetExpiredLeadsAsync()
    {
        return await _context
            .Leads.Where(l => l.IsActive && l.Status == LeadStatus.Expired)
            .Include(l => l.Client)
            .Include(l => l.AssignedTo)
            .Include(l => l.Project)
            .Include(l => l.Referral)
            .Include(l => l.LastRecycledBy)
            .ToListAsync();
    }

    public async Task<int> CheckAndUpdateExpiredLeadsAsync()
    {
        var now = DateTime.UtcNow;

        // Buscar leads activos con fecha de expiración anterior a ahora y que no estén ya marcados como expirados
        var expiredLeads = await _context
            .Leads.Where(l =>
                l.IsActive
                && l.ExpirationDate < now
                && l.Status != LeadStatus.Expired
                && l.Status != LeadStatus.Completed
                && l.Status != LeadStatus.Canceled
            )
            .ToListAsync();

        foreach (var lead in expiredLeads)
        {
            lead.Status = LeadStatus.Expired;
            lead.ModifiedAt = now;
        }

        await _context.SaveChangesAsync();

        return expiredLeads.Count;
    }

    public async Task<byte[]> ExportLeadsToExcelAsync(IExcelExportService excelExportService)
    {
        var leads = await GetAllLeadsAsync();

        // Diccionarios para traducción
        var statusLabels = new Dictionary<string, string>
        {
            { "Registered", "Registrado" },
            { "Attended", "Atendido" },
            { "InFollowUp", "En seguimiento" },
            { "Completed", "Completado" },
            { "Canceled", "Cancelado" },
            { "Expired", "Expirado" },
        };
        var captureSourceLabels = new Dictionary<string, string>
        {
            { "Company", "Empresa" },
            { "PersonalFacebook", "Facebook personal" },
            { "RealEstateFair", "Feria inmobiliaria" },
            { "Institutional", "Institucional" },
            { "Loyalty", "Fidelización" },
        };
        var completionReasonLabels = new Dictionary<string, string>
        {
            { "NotInterested", "No interesado" },
            { "InFollowUp", "En seguimiento" },
            { "Sale", "Venta concretada" },
        };

        // Define los encabezados visibles
        var headers = new List<string>
        {
            "Código",
            "Cliente",
            "DNI",
            "Teléfono",
            "Email",
            "Dirección",
            "País",
            "Tipo",
            "CoPropietarios",
            "Propiedad Separada",
            "Asesor",
            "Estado",
            "Medio de Captación",
            "Proyecto",
            "Motivo de finalización",
        };

        var data = new List<List<object>>();

        foreach (var lead in leads)
        {
            var client = lead.Client;
            var coOwners = client?.CoOwners ?? "";
            var separatePropertyData = client?.SeparatePropertyData ?? "";

            // Traducción de estado, medio de captación y motivo de finalización
            var estado = statusLabels.ContainsKey(lead.Status.ToString())
                ? statusLabels[lead.Status.ToString()]
                : lead.Status.ToString();
            var medioCaptacion = captureSourceLabels.ContainsKey(lead.CaptureSource.ToString())
                ? captureSourceLabels[lead.CaptureSource.ToString()]
                : lead.CaptureSource.ToString();
            string motivoFinalizacion = "";
            if (lead.CompletionReason != null)
            {
                var reasonStr = lead.CompletionReason.ToString();
                motivoFinalizacion = completionReasonLabels.ContainsKey(reasonStr)
                    ? completionReasonLabels[reasonStr]
                    : reasonStr;
            }

            data.Add(
                new List<object>
                {
                    lead.Code,
                    client?.Name!,
                    client?.Dni!,
                    client?.PhoneNumber!,
                    client?.Email!,
                    client?.Address!,
                    client?.Country!,
                    client?.Type!,
                    coOwners,
                    separatePropertyData,
                    lead.AssignedTo?.Name!,
                    estado,
                    medioCaptacion,
                    lead.Project?.Name!,
                    motivoFinalizacion,
                }
            );
        }

        // Indica qué columnas son datos complejos (coOwners y separatePropertyData)
        var complexIndexes = new List<int> { 8, 9 };

        return excelExportService.GenerateExcel(
            "Reporte de Leads",
            headers,
            data,
            true,
            complexIndexes
        );
    }

    /// <summary>
    /// Función inteligente para encontrar traducciones de roles automáticamente
    /// Detecta coincidencias parciales y exactas sin necesidad de agregar cada variación manualmente
    /// </summary>
    private static List<string> GetIntelligentRoleTranslations(
        string searchTerm,
        Dictionary<string, string> roleTranslation
    )
    {
        var translatedTerms = new List<string> { searchTerm };

        // 1. Traducciones exactas
        if (roleTranslation.ContainsKey(searchTerm))
        {
            translatedTerms.Add(roleTranslation[searchTerm].ToLower());
            return translatedTerms; // Si hay coincidencia exacta, solo devolver esa
        }

        // 2. Buscar coincidencias específicas en roles en español
        foreach (var kvp in roleTranslation)
        {
            var spanishRole = kvp.Key.ToLower();
            var englishRole = kvp.Value.ToLower();

            // Si el término de búsqueda está contenido en el rol en español
            if (spanishRole.Contains(searchTerm))
            {
                translatedTerms.Add(englishRole);
                return translatedTerms; // Si encontramos una coincidencia específica, solo devolver esa
            }
        }

        // 3. Solo si no hay coincidencias específicas, usar detección de patrones
        AddPatternBasedTranslations(searchTerm, translatedTerms);

        return translatedTerms.Distinct().ToList();
    }

    /// <summary>
    /// Detecta coincidencias inteligentes basadas en patrones comunes
    /// </summary>
    private static bool IsIntelligentMatch(
        string searchTerm,
        string spanishRole,
        string englishRole
    )
    {
        // Patrones comunes de abreviaciones y variaciones
        var patterns = new Dictionary<string, string[]>
        {
            { "ases", new[] { "asesor", "salesadvisor" } },
            { "admin", new[] { "administrador", "admin" } },
            { "super", new[] { "supervisor", "supervisor", "superadmin" } },
            { "ger", new[] { "gerente", "manager" } },
            { "fin", new[] { "finanzas", "finance" } },
            { "vent", new[] { "ventas", "sales" } },
        };

        foreach (var pattern in patterns)
        {
            if (searchTerm.Contains(pattern.Key))
            {
                if (
                    pattern.Value.Any(term =>
                        spanishRole.Contains(term) || englishRole.Contains(term)
                    )
                )
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Agrega traducciones basadas en patrones comunes
    /// </summary>
    private static void AddPatternBasedTranslations(string searchTerm, List<string> translatedTerms)
    {
        // Patrones de búsqueda inteligente
        if (searchTerm.Contains("ases") || searchTerm.Contains("vent"))
        {
            translatedTerms.Add("salesadvisor");
        }

        if (searchTerm.Contains("admin"))
        {
            translatedTerms.Add("admin");
            translatedTerms.Add("superadmin");
        }

        if (searchTerm.Contains("super"))
        {
            translatedTerms.Add("supervisor");
            translatedTerms.Add("superadmin");
        }

        if (searchTerm.Contains("ger"))
        {
            translatedTerms.Add("manager");
            translatedTerms.Add("financemanager");
        }

        if (searchTerm.Contains("fin"))
        {
            translatedTerms.Add("financemanager");
        }
    }
}
