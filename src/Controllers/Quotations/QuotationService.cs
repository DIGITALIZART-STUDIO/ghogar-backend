using System.Collections.Generic;
using GestionHogar.Configuration;
using GestionHogar.Dtos;
using GestionHogar.Model;
using GestionHogar.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GestionHogar.Services;

public class QuotationService(
    DatabaseContext _context,
    IEmailService _emailService,
    IOptions<BusinessInfo> _businessInfo,
    ILeadService _leadService
) : IQuotationService
{
    public async Task<IEnumerable<QuotationDTO>> GetAllQuotationsAsync()
    {
        var quotations = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l!.Client)
            .Include(q => q.Advisor)
            .Include(q => q.Lot) // Incluir el Lote
            .ThenInclude(l => l!.Block) // Incluir el Bloque relacionado con el Lote
            .ThenInclude(b => b.Project) // Incluir el Proyecto relacionado con el Bloque
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();

        return quotations.Select(QuotationDTO.FromEntity);
    }

    public async Task<QuotationDTO?> GetQuotationByIdAsync(Guid id)
    {
        var quotation = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l!.Client)
            .Include(q => q.Advisor)
            .Include(q => q.Lot) // **NUEVO: Incluir el lote**
            .ThenInclude(l => l!.Block)
            .ThenInclude(b => b.Project)
            .FirstOrDefaultAsync(q => q.Id == id);

        return quotation != null ? QuotationDTO.FromEntity(quotation) : null;
    }

    public async Task<QuotationDTO?> GetQuotationByReservationIdAsync(Guid reservationId)
    {
        var reservation = await _context
            .Reservations.Include(r => r.Quotation)
            .ThenInclude(q => q.Lead)
            .ThenInclude(l => l!.Client)
            .Include(r => r.Quotation)
            .ThenInclude(q => q.Advisor)
            .Include(r => r.Quotation)
            .ThenInclude(q => q.Lot)
            .ThenInclude(l => l!.Block)
            .ThenInclude(b => b.Project)
            .FirstOrDefaultAsync(r => r.Id == reservationId);

        if (reservation?.Quotation == null)
            return null;

        return QuotationDTO.FromEntity(reservation.Quotation);
    }

    public async Task<IEnumerable<QuotationDTO>> GetQuotationsByLeadIdAsync(Guid leadId)
    {
        var quotations = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l!.Client)
            .Include(q => q.Advisor)
            .Include(q => q.Lot)
            .ThenInclude(l => l!.Block)
            .ThenInclude(b => b.Project)
            .Where(q => q.LeadId == leadId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();

        return quotations.Select(QuotationDTO.FromEntity);
    }

    public async Task<IEnumerable<QuotationSummaryDTO>> GetQuotationsByAdvisorIdAsync(
        Guid advisorId
    )
    {
        var quotations = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l!.Client)
            .Include(q => q.Lot) // **NUEVO: Incluir el lote**
            .ThenInclude(l => l!.Block)
            .ThenInclude(b => b.Project)
            .Where(q => q.AdvisorId == advisorId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();

        return quotations.Select(QuotationSummaryDTO.FromEntity);
    }

    public async Task<
        PaginatedResponseV2<QuotationSummaryDTO>
    > GetQuotationsByAdvisorIdPaginatedAsync(
        Guid advisorId,
        int page,
        int pageSize,
        PaginationService paginationService,
        string? search = null,
        QuotationStatus[]? status = null,
        Guid[]? clientId = null,
        Guid? projectId = null,
        string? orderBy = null
    )
    {
        // Construir consulta base
        var query = _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l!.Client)
            .Include(q => q.Lot)
            .ThenInclude(l => l!.Block)
            .ThenInclude(b => b.Project)
            .Where(q => q.AdvisorId == advisorId);

        // Aplicar filtro de búsqueda si se proporciona
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.ToLower();
            query = query.Where(q =>
                (q.Code != null && q.Code.ToLower().Contains(searchTerm))
                || (
                    q.Lead != null
                    && q.Lead.Client != null
                    && q.Lead.Client.Name != null
                    && q.Lead.Client.Name.ToLower().Contains(searchTerm)
                )
                || (
                    q.Lead != null
                    && q.Lead.Client != null
                    && q.Lead.Client.Email != null
                    && q.Lead.Client.Email.ToLower().Contains(searchTerm)
                )
                || (
                    q.Lead != null
                    && q.Lead.Client != null
                    && q.Lead.Client.PhoneNumber != null
                    && q.Lead.Client.PhoneNumber.Contains(searchTerm)
                )
                || (
                    q.Lead != null
                    && q.Lead.Client != null
                    && q.Lead.Client.Dni != null
                    && q.Lead.Client.Dni.Contains(searchTerm)
                )
                || (
                    q.Lead != null
                    && q.Lead.Client != null
                    && q.Lead.Client.Ruc != null
                    && q.Lead.Client.Ruc.Contains(searchTerm)
                )
                || (
                    q.Lot != null
                    && q.Lot.Block != null
                    && q.Lot.Block.Project != null
                    && q.Lot.Block.Project.Name != null
                    && q.Lot.Block.Project.Name.ToLower().Contains(searchTerm)
                )
                || (
                    q.Lot != null
                    && q.Lot.LotNumber != null
                    && q.Lot.LotNumber.ToLower().Contains(searchTerm)
                )
            );
        }

        // Aplicar filtro de status si se proporciona
        if (status != null && status.Length > 0)
        {
            query = query.Where(q => status.Contains(q.Status));
        }

        // Aplicar filtro de clientId si se proporciona
        if (clientId != null && clientId.Length > 0)
        {
            query = query.Where(q =>
                q.Lead != null
                && q.Lead.ClientId.HasValue
                && clientId.Contains(q.Lead.ClientId.Value)
            );
        }

        // Filtro por proyecto
        if (projectId.HasValue)
        {
            query = query.Where(q =>
                q.Lot != null && q.Lot.Block != null && q.Lot.Block.ProjectId == projectId.Value
            );
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
                    ? query.OrderByDescending(q => q.Code)
                    : query.OrderBy(q => q.Code),
                "status" => direction == "desc"
                    ? query.OrderByDescending(q => q.Status)
                    : query.OrderBy(q => q.Status),
                "totalprice" => direction == "desc"
                    ? query.OrderByDescending(q => q.TotalPrice)
                    : query.OrderBy(q => q.TotalPrice),
                "finalprice" => direction == "desc"
                    ? query.OrderByDescending(q => q.FinalPrice)
                    : query.OrderBy(q => q.FinalPrice),
                "discount" => direction == "desc"
                    ? query.OrderByDescending(q => q.Discount)
                    : query.OrderBy(q => q.Discount),
                "downpayment" => direction == "desc"
                    ? query.OrderByDescending(q => q.DownPayment)
                    : query.OrderBy(q => q.DownPayment),
                "monthsfinanced" => direction == "desc"
                    ? query.OrderByDescending(q => q.MonthsFinanced)
                    : query.OrderBy(q => q.MonthsFinanced),
                "quotationdate" => direction == "desc"
                    ? query.OrderByDescending(q => q.QuotationDate)
                    : query.OrderBy(q => q.QuotationDate),
                "validuntil" => direction == "desc"
                    ? query.OrderByDescending(q => q.ValidUntil)
                    : query.OrderBy(q => q.ValidUntil),
                "clientname" => direction == "desc"
                    ? query.OrderByDescending(q =>
                        q.Lead != null
                            ? q.Lead.Client != null
                                ? q.Lead.Client.Name
                                : ""
                            : ""
                    )
                    : query.OrderBy(q =>
                        q.Lead != null
                            ? q.Lead.Client != null
                                ? q.Lead.Client.Name
                                : ""
                            : ""
                    ),
                "projectname" => direction == "desc"
                    ? query.OrderByDescending(q =>
                        q.Lot != null
                            ? q.Lot.Block != null
                                ? q.Lot.Block.Project != null
                                    ? q.Lot.Block.Project.Name
                                    : ""
                                : ""
                            : ""
                    )
                    : query.OrderBy(q =>
                        q.Lot != null
                            ? q.Lot.Block != null
                                ? q.Lot.Block.Project != null
                                    ? q.Lot.Block.Project.Name
                                    : ""
                                : ""
                            : ""
                    ),
                "lotnumber" => direction == "desc"
                    ? query.OrderByDescending(q => q.Lot != null ? q.Lot.LotNumber : "")
                    : query.OrderBy(q => q.Lot != null ? q.Lot.LotNumber : ""),
                "createdat" => direction == "desc"
                    ? query.OrderByDescending(q => q.CreatedAt)
                    : query.OrderBy(q => q.CreatedAt),
                "modifiedat" => direction == "desc"
                    ? query.OrderByDescending(q => q.ModifiedAt)
                    : query.OrderBy(q => q.ModifiedAt),
                _ => query.OrderByDescending(q => q.CreatedAt), // Ordenamiento por defecto
            };
        }
        else
        {
            // Ordenamiento por defecto
            query = query.OrderByDescending(q => q.CreatedAt);
        }

        // Aplicar paginación directamente a la query de entidades
        var paginatedResult = await paginationService.PaginateAsync(query, page, pageSize);

        // Convertir los datos a DTOs
        var quotationDtos = paginatedResult
            .Data.Select(q => QuotationSummaryDTO.FromEntity(q))
            .ToList();

        return new PaginatedResponseV2<QuotationSummaryDTO>
        {
            Data = quotationDtos,
            Meta = paginatedResult.Meta,
        };
    }

    public async Task<
        PaginatedResponseV2<QuotationSummaryDTO>
    > GetAcceptedQuotationsByAdvisorPaginatedAsync(
        Guid currentUserId,
        int page,
        int pageSize,
        string? search = null,
        string? orderBy = null,
        string? orderDirection = "asc",
        string? preselectedId = null
    )
    {
        // Lógica para preselectedId - incluir en la query base
        Guid? preselectedQuotationGuid = null;
        if (
            !string.IsNullOrWhiteSpace(preselectedId)
            && Guid.TryParse(preselectedId, out var parsedGuid)
        )
        {
            preselectedQuotationGuid = parsedGuid;
        }

        // Construir la query base
        var query = _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l!.Client)
            .Include(q => q.Lot)
            .ThenInclude(l => l!.Block)
            .ThenInclude(b => b.Project)
            .Where(q => q.Status == QuotationStatus.ACCEPTED)
            // Solo cotizaciones del usuario actual
            .Where(q => q.AdvisorId == currentUserId)
            // Excluir cotizaciones que ya tienen reservas activas
            .Where(q => !_context.Reservations.Any(r => r.QuotationId == q.Id && r.IsActive));

        // Aplicar filtro de búsqueda si se proporciona
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(q =>
                (
                    q.Lead != null
                    && q.Lead.Client != null
                    && q.Lead.Client.Name != null
                    && q.Lead.Client.Name.ToLower().Contains(searchLower)
                )
                || (
                    q.Lead != null
                    && q.Lead.Client != null
                    && q.Lead.Client.Dni != null
                    && q.Lead.Client.Dni.ToLower().Contains(searchLower)
                )
                || (
                    q.Lead != null
                    && q.Lead.Client != null
                    && q.Lead.Client.Ruc != null
                    && q.Lead.Client.Ruc.ToLower().Contains(searchLower)
                )
                || (
                    q.Lead != null
                    && q.Lead.Client != null
                    && q.Lead.Client.PhoneNumber != null
                    && q.Lead.Client.PhoneNumber.ToLower().Contains(searchLower)
                )
                || (
                    q.Lot != null
                    && q.Lot.Block != null
                    && q.Lot.Block.Project != null
                    && q.Lot.Block.Project.Name != null
                    && q.Lot.Block.Project.Name.ToLower().Contains(searchLower)
                )
                || (
                    q.Lot != null
                    && q.Lot.LotNumber != null
                    && q.Lot.LotNumber.ToLower().Contains(searchLower)
                )
            );
        }

        // Aplicar ordenamiento
        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            var isDescending = orderDirection?.ToLower() == "desc";

            // Si hay preselectedId en la primera página, mantenerlo primero
            if (preselectedQuotationGuid.HasValue && page == 1)
            {
                query = orderBy.ToLower() switch
                {
                    "clientname" => isDescending
                        ? query
                            .OrderBy(q => q.Id == preselectedQuotationGuid ? 0 : 1)
                            .ThenByDescending(q =>
                                q.Lead != null
                                    ? q.Lead.Client != null
                                        ? q.Lead.Client.Name
                                        : ""
                                    : ""
                            )
                        : query
                            .OrderBy(q => q.Id == preselectedQuotationGuid ? 0 : 1)
                            .ThenBy(q =>
                                q.Lead != null
                                    ? q.Lead.Client != null
                                        ? q.Lead.Client.Name
                                        : ""
                                    : ""
                            ),
                    "projectname" => isDescending
                        ? query
                            .OrderBy(q => q.Id == preselectedQuotationGuid ? 0 : 1)
                            .ThenByDescending(q =>
                                q.Lot != null
                                    ? q.Lot.Block != null
                                        ? q.Lot.Block.Project != null
                                            ? q.Lot.Block.Project.Name
                                            : ""
                                        : ""
                                    : ""
                            )
                        : query
                            .OrderBy(q => q.Id == preselectedQuotationGuid ? 0 : 1)
                            .ThenBy(q =>
                                q.Lot != null
                                    ? q.Lot.Block != null
                                        ? q.Lot.Block.Project != null
                                            ? q.Lot.Block.Project.Name
                                            : ""
                                        : ""
                                    : ""
                            ),
                    "lotcode" => isDescending
                        ? query
                            .OrderBy(q => q.Id == preselectedQuotationGuid ? 0 : 1)
                            .ThenByDescending(q => q.Lot != null ? q.Lot.LotNumber : "")
                        : query
                            .OrderBy(q => q.Id == preselectedQuotationGuid ? 0 : 1)
                            .ThenBy(q => q.Lot != null ? q.Lot.LotNumber : ""),
                    "createdat" => isDescending
                        ? query
                            .OrderBy(q => q.Id == preselectedQuotationGuid ? 0 : 1)
                            .ThenByDescending(q => q.CreatedAt)
                        : query
                            .OrderBy(q => q.Id == preselectedQuotationGuid ? 0 : 1)
                            .ThenBy(q => q.CreatedAt),
                    _ => query
                        .OrderBy(q => q.Id == preselectedQuotationGuid ? 0 : 1)
                        .ThenByDescending(q => q.CreatedAt),
                };
            }
            else
            {
                query = orderBy.ToLower() switch
                {
                    "clientname" => isDescending
                        ? query.OrderByDescending(q =>
                            q.Lead != null
                                ? q.Lead.Client != null
                                    ? q.Lead.Client.Name
                                    : ""
                                : ""
                        )
                        : query.OrderBy(q =>
                            q.Lead != null
                                ? q.Lead.Client != null
                                    ? q.Lead.Client.Name
                                    : ""
                                : ""
                        ),
                    "projectname" => isDescending
                        ? query.OrderByDescending(q =>
                            q.Lot != null
                                ? q.Lot.Block != null
                                    ? q.Lot.Block.Project != null
                                        ? q.Lot.Block.Project.Name
                                        : ""
                                    : ""
                                : ""
                        )
                        : query.OrderBy(q =>
                            q.Lot != null
                                ? q.Lot.Block != null
                                    ? q.Lot.Block.Project != null
                                        ? q.Lot.Block.Project.Name
                                        : ""
                                    : ""
                                : ""
                        ),
                    "lotcode" => isDescending
                        ? query.OrderByDescending(q => q.Lot != null ? q.Lot.LotNumber : "")
                        : query.OrderBy(q => q.Lot != null ? q.Lot.LotNumber : ""),
                    "createdat" => isDescending
                        ? query.OrderByDescending(q => q.CreatedAt)
                        : query.OrderBy(q => q.CreatedAt),
                    _ => query.OrderByDescending(q => q.CreatedAt), // Ordenamiento por defecto
                };
            }
        }
        else
        {
            // Ordenamiento por defecto
            if (preselectedQuotationGuid.HasValue && page == 1)
            {
                query = query
                    .OrderBy(q => q.Id == preselectedQuotationGuid ? 0 : 1)
                    .ThenByDescending(q => q.CreatedAt);
            }
            else
            {
                query = query.OrderByDescending(q => q.CreatedAt);
            }
        }

        // Lógica para preselectedId - incluir en la query base
        if (preselectedQuotationGuid.HasValue)
        {
            if (page == 1)
            {
                // En la primera página: incluir la cotización preseleccionada al inicio
                var preselectedQuotation = await _context
                    .Quotations.Include(q => q.Lead)
                    .ThenInclude(l => l!.Client)
                    .Include(q => q.Lot)
                    .ThenInclude(l => l!.Block)
                    .ThenInclude(b => b.Project)
                    .FirstOrDefaultAsync(q =>
                        q.Id == preselectedQuotationGuid
                        && q.Status == QuotationStatus.ACCEPTED
                        && q.AdvisorId == currentUserId
                        && !_context.Reservations.Any(r => r.QuotationId == q.Id && r.IsActive)
                    );

                if (preselectedQuotation != null)
                {
                    // Modificar la query para que la cotización preseleccionada aparezca primero
                    query = query.OrderBy(q => q.Id == preselectedQuotationGuid ? 0 : 1);
                }
            }
            else
            {
                // En páginas siguientes: excluir la cotización preseleccionada para evitar duplicados
                query = query.Where(q => q.Id != preselectedQuotationGuid);
            }
        }

        // Aplicar paginación
        var totalCount = await query.CountAsync();
        var quotations = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        // Convertir a DTOs
        var quotationDtos = quotations.Select(QuotationSummaryDTO.FromEntity).ToList();

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

        return new PaginatedResponseV2<QuotationSummaryDTO>
        {
            Data = quotationDtos,
            Meta = paginationMetadata,
        };
    }

    // **NUEVO: Obtener cotizaciones por lote**
    public async Task<IEnumerable<QuotationDTO>> GetQuotationsByLotIdAsync(Guid lotId)
    {
        var quotations = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l!.Client)
            .Include(q => q.Advisor)
            .Include(q => q.Lot)
            .ThenInclude(l => l!.Block)
            .ThenInclude(b => b.Project)
            .Where(q => q.LotId == lotId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();

        return quotations.Select(QuotationDTO.FromEntity);
    }

    // **NUEVO: Obtener cotizaciones por proyecto**
    public async Task<IEnumerable<QuotationDTO>> GetQuotationsByProjectIdAsync(Guid projectId)
    {
        var quotations = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l!.Client)
            .Include(q => q.Advisor)
            .Include(q => q.Lot)
            .ThenInclude(l => l!.Block)
            .ThenInclude(b => b.Project)
            .Where(q => q.Lot!.Block.ProjectId == projectId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();

        return quotations.Select(QuotationDTO.FromEntity);
    }

    private static string GetLotStatusSpanish(LotStatus status)
    {
        return status switch
        {
            LotStatus.Available => "Disponible",
            LotStatus.Quoted => "Cotizado",
            LotStatus.Reserved => "Reservado",
            LotStatus.Sold => "Vendido",
            _ => status.ToString(),
        };
    }

    public async Task<QuotationDTO> CreateQuotationAsync(
        QuotationCreateDTO dto,
        Guid currentUserId,
        IEnumerable<string> currentUserRoles
    )
    {
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

        if (hasHigherRole)
        {
            // Para usuarios con roles mayores, verificar si LeadId es Lead o Client
            var leadExists = await LeadExistsAsync(dto.LeadId);

            if (!leadExists)
            {
                // Verificar si existe como Cliente
                var clientExists = await ClientExistsAsync(dto.LeadId);

                if (clientExists)
                {
                    // Crear un nuevo Lead con el cliente y asignarlo al usuario actual
                    var newLeadId = await CreateLeadFromClientAsync(dto.LeadId, currentUserId);

                    // Actualizar el DTO con el nuevo LeadId
                    dto.LeadId = newLeadId;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"No se encontró un Lead con ID {dto.LeadId}"
                    );
                }
            }
        }
        else
        {
            // Para SalesAdvisor, validar que el Lead existe
            var leadExists = await LeadExistsAsync(dto.LeadId);
            if (!leadExists)
            {
                throw new InvalidOperationException($"Lead con ID {dto.LeadId} no encontrado");
            }
        }

        // Verificar que el lead existe (después de la validación/creación)
        var lead = await _context.Leads.FindAsync(dto.LeadId);
        if (lead == null)
            throw new InvalidOperationException($"Lead con ID {dto.LeadId} no encontrado");

        // Verificar que el asesor (usuario actual) existe
        var advisor = await _context.Users.FindAsync(currentUserId);
        if (advisor == null)
            throw new InvalidOperationException($"Usuario con ID {currentUserId} no encontrado");

        // **NUEVO: Validar que el lote existe y está disponible**
        var lot = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .FirstOrDefaultAsync(l => l.Id == dto.LotId);

        if (lot == null)
            throw new InvalidOperationException($"Lote con ID {dto.LotId} no encontrado");

        if (lot.Status == LotStatus.Reserved)
            throw new InvalidOperationException(
                "No se puede crear una cotización para un lote que está reservado"
            );

        if (lot.Status != LotStatus.Available && lot.Status != LotStatus.Quoted)
            throw new InvalidOperationException(
                $"El lote no está disponible para cotizar. Estado actual: {GetLotStatusSpanish(lot.Status)}"
            );

        if (!lot.IsActive || !lot.Block.IsActive || !lot.Block.Project.IsActive)
            throw new InvalidOperationException("El lote, bloque o proyecto no está activo");

        // Generar código automáticamente
        var code = await GenerateQuotationCodeAsync();

        // **NUEVO: Crear cotización con datos del lote y usuario actual como asesor**
        var quotation = dto.ToEntity(code, lot);
        quotation.AdvisorId = currentUserId; // Usar el usuario actual como asesor
        _context.Quotations.Add(quotation);

        // **NUEVO: Cambiar estado del lote a Quoted**
        lot.Status = LotStatus.Quoted;
        lot.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Recargar la cotización con relaciones incluidas
        var createdQuotation = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l!.Client)
            .Include(q => q.Advisor)
            .Include(q => q.Lot)
            .ThenInclude(l => l!.Block)
            .ThenInclude(b => b.Project)
            .FirstAsync(q => q.Id == quotation.Id);

        return QuotationDTO.FromEntity(createdQuotation);
    }

    public async Task<QuotationDTO?> UpdateQuotationAsync(Guid id, QuotationUpdateDTO dto)
    {
        var quotation = await _context
            .Quotations.Include(q => q.Lot) // **NUEVO: Incluir el lote para validaciones**
            .ThenInclude(l => l.Block)
            .ThenInclude(b => b.Project)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quotation == null)
            return null;

        // Validar que el lote está activo
        if (
            quotation.Lot == null
            || !quotation.Lot.IsActive
            || !quotation.Lot.Block.IsActive
            || !quotation.Lot.Block.Project.IsActive
        )
            throw new InvalidOperationException("El lote, bloque o proyecto no está activo");

        // Validar que el estado del lote permite actualización (ejemplo: solo si está Cotizado o Disponible)
        if (quotation.Lot.Status != LotStatus.Available && quotation.Lot.Status != LotStatus.Quoted)
            throw new InvalidOperationException(
                $"No se puede actualizar la cotización porque el lote no está disponible. Estado actual: {GetLotStatusSpanish(quotation.Lot.Status)}"
            );

        // Si se cambia el asesor, verificar que existe
        if (dto.AdvisorId.HasValue && dto.AdvisorId.Value != quotation.AdvisorId)
        {
            var advisor = await _context.Users.FindAsync(dto.AdvisorId.Value);
            if (advisor == null)
                throw new InvalidOperationException("Asesor no encontrado");
        }

        dto.ApplyTo(quotation);
        await _context.SaveChangesAsync();

        // Recargar la cotización con relaciones incluidas
        var updatedQuotation = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l!.Client)
            .Include(q => q.Advisor)
            .Include(q => q.Lot)
            .ThenInclude(l => l!.Block)
            .ThenInclude(b => b.Project)
            .FirstAsync(q => q.Id == id);

        return QuotationDTO.FromEntity(updatedQuotation);
    }

    public async Task<bool> DeleteQuotationAsync(Guid id)
    {
        var quotation = await _context
            .Quotations.Include(q => q.Lot) // **NUEVO: Incluir el lote**
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quotation == null)
            return false;

        // **NUEVO: Liberar el lote si la cotización estaba activa**
        if (quotation.Status == QuotationStatus.ISSUED && quotation.Lot != null)
        {
            quotation.Lot.Status = LotStatus.Available;
            quotation.Lot.ModifiedAt = DateTime.UtcNow;
        }

        _context.Quotations.Remove(quotation);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<QuotationDTO?> ChangeStatusAsync(Guid id, string status)
    {
        var quotation = await _context
            .Quotations.Include(q => q.Lot) // **NUEVO: Incluir el lote**
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quotation == null || !Enum.TryParse<QuotationStatus>(status, true, out var statusEnum))
            return null;

        var oldStatus = quotation.Status;
        quotation.Status = statusEnum;
        quotation.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Recargar la cotización con relaciones incluidas
        var updatedQuotation = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l!.Client)
            .Include(q => q.Advisor)
            .Include(q => q.Lot)
            .ThenInclude(l => l!.Block)
            .ThenInclude(b => b.Project)
            .FirstAsync(q => q.Id == id);

        return QuotationDTO.FromEntity(updatedQuotation);
    }

    // **NUEVO: Método para liberar un lote (volver a Available)**
    public async Task<bool> ReleaseLotAsync(Guid quotationId)
    {
        var quotation = await _context
            .Quotations.Include(q => q.Lot)
            .FirstOrDefaultAsync(q => q.Id == quotationId);

        if (quotation?.Lot == null)
            return false;

        quotation.Status = QuotationStatus.CANCELED;
        quotation.Lot.Status = LotStatus.Available;
        quotation.ModifiedAt = DateTime.UtcNow;
        quotation.Lot.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<string> GenerateQuotationCodeAsync()
    {
        // Formato: COT-YYYY-XXXXX donde YYYY es el año actual y XXXXX es un número secuencial
        int year = DateTime.UtcNow.Year;
        var yearPrefix = $"COT-{year}-";

        // Buscar el último código generado este año
        var lastQuotation = await _context
            .Quotations.Where(q => q.Code.StartsWith(yearPrefix))
            .OrderByDescending(q => q.Code)
            .FirstOrDefaultAsync();

        int sequence = 1;
        if (lastQuotation != null)
        {
            // Extraer y aumentar la secuencia
            var parts = lastQuotation.Code.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out int lastSequence))
            {
                sequence = lastSequence + 1;
            }
        }

        return $"{yearPrefix}{sequence:D5}";
    }

    public async Task<byte[]> GenerateQuotationPdfAsync(Guid quotationId)
    {
        var quotation = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(lead => lead.Client)
            .Include(q => q.Lot)
            .ThenInclude(lot => lot.Block)
            .ThenInclude(block => block.Project)
            .Include(q => q.Lead)
            .ThenInclude(lead => lead.AssignedTo)
            .FirstOrDefaultAsync(q => q.Id == quotationId);

        if (quotation is null)
        {
            throw new Exception("Quotation not found");
        }

        var client = quotation.Lead?.Client;
        var exchangeRate = quotation.ExchangeRate;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Header()
                    .Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(2);
                        });

                        table.Cell();
                        table.Cell().AlignCenter().Text("COTIZACIÓN").FontSize(24).ExtraBold();
                        table.Cell();
                    });

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(x =>
                    {
                        x.Spacing(0);

                        // Cliente, Copropietario
                        x.Item()
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                });

                                table.Cell();
                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Grey.Darken1)
                                    .Padding(2)
                                    .AlignCenter()
                                    .Text($"T.C. REFERENCIAL: {exchangeRate.ToString()}");
                            });

                        // Cliente, Copropietario
                        x.Item()
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                });

                                table
                                    .Cell()
                                    .PaddingRight(16)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Darken1)
                                    .PaddingTop(2)
                                    .PaddingBottom(2)
                                    .AlignLeft()
                                    .Text($"Cliente: {client?.Name ?? "-"}");
                                table
                                    .Cell()
                                    .PaddingTop(2)
                                    .PaddingBottom(2)
                                    .AlignLeft()
                                    .Text("DETALLES DE FINANCIAMIENTO")
                                    .Bold();
                                table
                                    .Cell()
                                    .PaddingRight(16)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Darken1)
                                    .PaddingTop(2)
                                    .PaddingBottom(2)
                                    .AlignLeft()
                                    .Text(text =>
                                    {
                                        text.Span("Copropietario: ");

                                        // Procesar el JSON de co-owners para extraer solo el nombre del primero
                                        string coOwnerName = "-";
                                        if (!string.IsNullOrEmpty(client?.CoOwners))
                                        {
                                            try
                                            {
                                                // Deserializar el JSON a un elemento JsonElement
                                                var coOwners =
                                                    System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                                                        client.CoOwners
                                                    );

                                                // Verificar que sea un array y tenga al menos un elemento
                                                if (
                                                    coOwners.ValueKind
                                                        == System.Text.Json.JsonValueKind.Array
                                                    && coOwners.GetArrayLength() > 0
                                                )
                                                {
                                                    // Obtener el primer elemento
                                                    var firstCoOwner = coOwners[0];

                                                    // Intentar obtener la propiedad "name" del primer co-owner
                                                    if (
                                                        firstCoOwner.TryGetProperty(
                                                            "name",
                                                            out var nameElement
                                                        )
                                                    )
                                                    {
                                                        coOwnerName =
                                                            nameElement.GetString() ?? "-";
                                                    }
                                                }
                                            }
                                            catch
                                            {
                                                // En caso de error al procesar el JSON, dejar el valor por defecto
                                                coOwnerName = "-";
                                            }
                                        }

                                        text.Span(coOwnerName);
                                    });
                            });

                        // DNI, Celular
                        x.Item()
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(4);
                                    columns.RelativeColumn(5);
                                    columns.RelativeColumn(6);
                                });

                                table
                                    .Cell()
                                    .PaddingRight(16)
                                    .Element(DataCellStyle)
                                    .Text($"DNI: {client?.Dni ?? "-"}");
                                table
                                    .Cell()
                                    .PaddingRight(16)
                                    .Element(DataCellStyle)
                                    .Text($"Celular: {client?.PhoneNumber ?? "-"}");
                                table
                                    .Cell()
                                    .Element(DataCellStyle)
                                    .Text($"Nro de Meses: {quotation.MonthsFinanced}");

                                static IContainer DataCellStyle(IContainer container)
                                {
                                    return container
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Grey.Darken1)
                                        .PaddingTop(2)
                                        .PaddingBottom(2)
                                        .AlignLeft();
                                }
                            });

                        // Email, Proyecto
                        x.Item()
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                });

                                table
                                    .Cell()
                                    .PaddingRight(16)
                                    .Element(DataCellStyle)
                                    .Text($"Email: {client?.Email ?? "-"}");
                                table.Cell().Element(DataCellStyle).Text("Fecha de pago/cuota");

                                table
                                    .Cell()
                                    .PaddingRight(16)
                                    .Element(DataCellStyle)
                                    .Text($"Proyecto: {quotation.Lot.Block.Project.Name}");
                                table
                                    .Cell()
                                    .Element(DataCellStyle)
                                    .Text($"Fecha de Cotización: {quotation.QuotationDate}");

                                static IContainer DataCellStyle(IContainer container)
                                {
                                    return container
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Grey.Darken1)
                                        .PaddingTop(2)
                                        .PaddingBottom(2)
                                        .AlignLeft();
                                }
                            });

                        x.Item()
                            .PaddingTop(30)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1); // Lugar
                                    columns.RelativeColumn(1); // Area
                                    columns.RelativeColumn(1); // Precio por m2
                                    columns.RelativeColumn(1); // Precio de lista
                                    columns.RelativeColumn(1); // Precio en soles
                                });

                                // Header row with all borders
                                table.Header(header =>
                                {
                                    header
                                        .Cell()
                                        .Element(HeaderCellStyle)
                                        .AlignCenter()
                                        .Padding(5)
                                        .Text("Mz - Lote");
                                    header
                                        .Cell()
                                        .Element(HeaderCellStyle)
                                        .AlignCenter()
                                        .Padding(5)
                                        .Text("Area");
                                    header
                                        .Cell()
                                        .Element(HeaderCellStyle)
                                        .AlignCenter()
                                        .Padding(5)
                                        .Text("Precio por m²");
                                    header
                                        .Cell()
                                        .Element(HeaderCellStyle)
                                        .AlignCenter()
                                        .Padding(5)
                                        .Text("Precio de lista");
                                    header
                                        .Cell()
                                        .Element(HeaderCellStyle)
                                        .AlignCenter()
                                        .Padding(5)
                                        .Text("Precio en soles");

                                    static IContainer HeaderCellStyle(IContainer container)
                                    {
                                        return container
                                            .DefaultTextStyle(x => x.SemiBold())
                                            .PaddingVertical(0)
                                            .PaddingHorizontal(0)
                                            .Border(1)
                                            .BorderColor(Colors.Grey.Darken1)
                                            .Background(Colors.White);
                                    }
                                });

                                // Sample data rows with all borders
                                var tableData = new[]
                                {
                                    new
                                    {
                                        Lugar = $"{quotation.Lot.Block.Name} - {quotation.Lot.LotNumber}",
                                        Area = $"{quotation.AreaAtQuotation} m2",
                                        PrecioM2 = $"{quotation.PricePerM2AtQuotation}",
                                        PrecioLista = $"{quotation.TotalPrice}",
                                        PrecioSoles = "S/ 95,250.00",
                                    },
                                };

                                foreach (var row in tableData)
                                {
                                    table.Cell().Element(DataCellStyle).Text(row.Lugar);
                                    table.Cell().Element(DataCellStyle).Text(row.Area);
                                    table.Cell().Element(DataCellStyle).Text(row.PrecioM2);
                                    table.Cell().Element(DataCellStyle).Text(row.PrecioLista);
                                    table.Cell().Element(DataCellStyle).Text(row.PrecioSoles);

                                    static IContainer DataCellStyle(IContainer container)
                                    {
                                        return container
                                            .PaddingVertical(0)
                                            .PaddingHorizontal(0)
                                            .Border(1)
                                            .BorderColor(Colors.Grey.Darken1)
                                            .AlignCenter()
                                            .Padding(5);
                                    }
                                }
                            });

                        // DSC. P.Lista
                        x.Item()
                            .PaddingTop(30)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                });

                                table.Cell();
                                table.Cell();
                                table.Cell();
                                table
                                    .Cell()
                                    .ColumnSpan(2)
                                    .Border(1)
                                    .BorderColor(Colors.Grey.Darken1)
                                    .Padding(5)
                                    .AlignCenter()
                                    .Text("Precio de venta");

                                table
                                    .Cell()
                                    .ColumnSpan(2)
                                    .Element(DataCellStyle)
                                    .AlignCenter()
                                    .Text("DSC P.LISTA");
                                table
                                    .Cell()
                                    .Element(DataCellStyle)
                                    .AlignLeft()
                                    .PaddingLeft(10)
                                    .Text($"$ {quotation.Discount.ToString()}");
                                table
                                    .Cell()
                                    .Element(DataCellStyle)
                                    .AlignLeft()
                                    .PaddingLeft(10)
                                    .Text($"$ {quotation.FinalPrice}");
                                table
                                    .Cell()
                                    .Element(DataCellStyle)
                                    .AlignLeft()
                                    .PaddingLeft(10)
                                    .Text(
                                        $"S/ {(quotation.FinalPrice * exchangeRate).ToString("N2")}"
                                    );

                                table.Cell().ColumnSpan(5).Text("");

                                //
                                // Inicial
                                //
                                var downPaymentDollars =
                                    (quotation.DownPayment / 100) * quotation.FinalPrice;
                                var downPaymentSoles = downPaymentDollars * exchangeRate;
                                table.Cell().Element(DataCellStyleThin).Text("Inicial");
                                table
                                    .Cell()
                                    .Element(DataCellStyleThin)
                                    .AlignRight()
                                    .Text($"{quotation.DownPayment} %");
                                table.Cell();
                                table
                                    .Cell()
                                    .Element(DataCellStyleThin)
                                    .Text($"$ {downPaymentDollars.ToString("N2")}");
                                table
                                    .Cell()
                                    .Element(DataCellStyleThin)
                                    .Text($"S/ {downPaymentSoles.ToString("N2")}");

                                table.Cell().ColumnSpan(5).Text("");

                                //
                                // A Financiar
                                //
                                var financingPercentange = 100 - quotation.DownPayment;
                                var financingAmountDollars =
                                    quotation.FinalPrice - downPaymentDollars;
                                var financingAmountSoles = financingAmountDollars * exchangeRate;
                                table.Cell().Element(DataCellStyleThin).Text("A financiar");
                                table
                                    .Cell()
                                    .Element(DataCellStyleThin)
                                    .AlignRight()
                                    .Text($"{financingPercentange} %");
                                table.Cell();
                                table
                                    .Cell()
                                    .Element(DataCellStyleThin)
                                    .Text($"$ {financingAmountDollars.ToString("N2")}");
                                table
                                    .Cell()
                                    .Element(DataCellStyleThin)
                                    .Text($"S/ {financingAmountSoles.ToString("N2")}");

                                table.Cell().ColumnSpan(5).Text("");

                                //
                                // Cuotas
                                //
                                var monthlyPaymentDollars =
                                    financingAmountDollars / quotation.MonthsFinanced;
                                var monthlyPaymentSoles = monthlyPaymentDollars * exchangeRate;
                                table.Cell();
                                table
                                    .Cell()
                                    .Element(DataCellStyleThin)
                                    .AlignCenter()
                                    .Text(quotation.MonthsFinanced.ToString());
                                table
                                    .Cell()
                                    .Element(DataCellStyleThin)
                                    .AlignCenter()
                                    .Text("Cuotas de");
                                table
                                    .Cell()
                                    .Element(DataCellStyleThin)
                                    .Text($"$ {monthlyPaymentDollars.ToString("N2")}");
                                table
                                    .Cell()
                                    .Element(DataCellStyleThin)
                                    .Text($"S/ {monthlyPaymentSoles.ToString("N2")}");

                                table.Cell().ColumnSpan(5).Text("");
                                table.Cell().ColumnSpan(5).Text("");

                                table.Cell().Element(DataCellStyleThin).Text("Asesor");
                                table
                                    .Cell()
                                    .ColumnSpan(2)
                                    .Element(DataCellStyleThin)
                                    .Text($"{quotation.Lead?.AssignedTo?.Name ?? "-"}");
                                table.Cell().ColumnSpan(2);

                                table.Cell().Element(DataCellStyleThin).Text("Celular");
                                table
                                    .Cell()
                                    .ColumnSpan(2)
                                    .Element(DataCellStyleThin)
                                    .Text($"{quotation.Lead?.AssignedTo?.PhoneNumber ?? "-"}");
                                table.Cell().ColumnSpan(2);

                                table
                                    .Cell()
                                    .ColumnSpan(5)
                                    .PaddingTop(4)
                                    .PaddingBottom(4)
                                    .Text("*Cotizacion válida por 5 días")
                                    .FontColor(Colors.Grey.Darken1);

                                static IContainer DataCellStyle(IContainer container)
                                {
                                    return container
                                        .Border(1)
                                        .BorderColor(Colors.Grey.Darken1)
                                        .Padding(5)
                                        .PaddingTop(8)
                                        .PaddingBottom(8);
                                }
                                static IContainer DataCellStyleThin(IContainer container)
                                {
                                    return container
                                        .Border(1)
                                        .BorderColor(Colors.Grey.Darken1)
                                        .PaddingTop(2)
                                        .PaddingBottom(2)
                                        .PaddingLeft(10)
                                        .PaddingRight(10);
                                }
                            });
                    });

                page.Footer().AlignCenter().Text("-- Logos --");
            });
        });

        return document.GeneratePdf();
    }

    // Métodos OTP Implementation

    /// <summary>
    /// Envía un código OTP por email al usuario especificado.
    /// Invalida cualquier código OTP previo del usuario.
    /// </summary>
    public async Task<SendOtpResponseDto> SendOtpToUserAsync(Guid supervisorId, Guid asesorId)
    {
        const string purpose = "Desbloquear Descuento";

        // Verifica que el supervisor existe y está activo
        var supervisor = await _context.Users.FirstOrDefaultAsync(u =>
            u.Id == supervisorId && u.IsActive
        );
        if (supervisor == null)
            return new SendOtpResponseDto
            {
                Success = false,
                Message = "Supervisor/Admin no encontrado o inactivo",
            };

        // Verifica que el supervisor tenga el rol adecuado
        var supervisorRoles = await _context
            .UserRoles.Where(ur => ur.UserId == supervisorId)
            .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
            .ToListAsync();

        if (
            !supervisorRoles.Any(r =>
                r == "Admin"
                || r == "Supervisor"
                || r == "Manager"
                || r == "FinanceManager"
                || r == "SuperAdmin"
            )
        )
            return new SendOtpResponseDto
            {
                Success = false,
                Message = "Solo un Admin, Supervisor o Gerente puede recibir el OTP",
            };

        // Verifica que el asesor existe y está activo
        var asesor = await _context.Users.FirstOrDefaultAsync(u => u.Id == asesorId && u.IsActive);
        if (asesor == null)
            return new SendOtpResponseDto
            {
                Success = false,
                Message = "Asesor no encontrado o inactivo",
            };

        if (string.IsNullOrEmpty(supervisor.Email))
            return new SendOtpResponseDto
            {
                Success = false,
                Message = "El supervisor no tiene email configurado",
            };

        // Invalida OTPs previos para ese supervisor, asesor y propósito
        var existingOtps = await _context
            .OtpCodes.Where(o =>
                o.UserId == supervisorId
                && o.RequestedByUserId == asesorId
                && o.Purpose == purpose
                && o.IsActive
            )
            .ToListAsync();

        foreach (var otp in existingOtps)
            otp.Invalidate();

        // Genera nuevo OTP
        var otpCode = OtpCode.GenerateOtpCode();
        var expirationTime = DateTime.UtcNow.AddMinutes(5);

        var newOtp = new OtpCode
        {
            UserId = supervisorId,
            RequestedByUserId = asesorId,
            Purpose = purpose,
            Code = otpCode,
            ExpiresAt = expirationTime,
        };

        _context.OtpCodes.Add(newOtp);
        await _context.SaveChangesAsync();

        // Envía el email al supervisor/admin
        var emailContent = GenerateOtpEmailContent(supervisor.Name, otpCode, asesor.Name);

        var emailRequest = new EmailRequest
        {
            To = supervisor.Email,
            Subject = "Código de autorización OTP - Gestión Hogar",
            Content = emailContent,
        };

        var emailSent = await _emailService.SendEmailAsync(emailRequest);

        if (!emailSent)
            return new SendOtpResponseDto
            {
                Success = false,
                Message = "Error al enviar el email con el código OTP",
            };

        return new SendOtpResponseDto
        {
            Success = true,
            Message = "Código OTP enviado correctamente al supervisor/admin",
            ExpiresAt = expirationTime,
        };
    }

    /// <summary>
    /// Verifica un código OTP para un usuario específico
    /// </summary>
    public async Task<VerifyOtpResponseDto> VerifyOtpAsync(Guid userId, string otpCode)
    {
        try
        {
            // Verificar que el usuario existe y está activo
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
            if (user == null)
            {
                return new VerifyOtpResponseDto
                {
                    Success = false,
                    Message =
                        "El usuario que intenta validar el código OTP no existe o está inactivo.",
                };
            }

            // Buscar el código OTP más reciente y activo para el usuario y propósito fijo
            const string purpose = "Desbloquear Descuento";
            var otpRecord = await _context
                .OtpCodes.Where(o =>
                    o.UserId == userId && o.Code == otpCode && o.Purpose == purpose
                )
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (otpRecord == null)
            {
                return new VerifyOtpResponseDto
                {
                    Success = false,
                    Message =
                        "El código OTP ingresado no es válido o no corresponde a una solicitud activa.",
                };
            }

            // Validaciones robustas
            if (!otpRecord.IsActive)
            {
                return new VerifyOtpResponseDto
                {
                    Success = false,
                    Message =
                        "Este código OTP ya no está activo. Solicite uno nuevo si es necesario.",
                };
            }

            if (otpRecord.IsUsed)
            {
                return new VerifyOtpResponseDto
                {
                    Success = false,
                    Message =
                        "Este código OTP ya ha sido utilizado. Solicite uno nuevo si requiere autorización.",
                };
            }

            if (DateTime.UtcNow > otpRecord.ExpiresAt)
            {
                otpRecord.Invalidate();
                await _context.SaveChangesAsync();
                return new VerifyOtpResponseDto
                {
                    Success = false,
                    Message = "El código OTP ha expirado. Solicite uno nuevo para continuar.",
                };
            }

            // Marcar el código como usado y registrar quién lo aprobó
            otpRecord.MarkAsUsed(userId);
            await _context.SaveChangesAsync();

            return new VerifyOtpResponseDto
            {
                Success = true,
                Message =
                    "Código OTP verificado correctamente. La autorización ha sido registrada.",
            };
        }
        catch (Exception ex)
        {
            return new VerifyOtpResponseDto
            {
                Success = false,
                Message = $"Error interno del servidor: {ex.Message}",
            };
        }
    }

    // Nuevos métodos para validación de leads y clientes
    public async Task<bool> LeadExistsAsync(Guid leadId)
    {
        return await _context.Leads.AnyAsync(l => l.Id == leadId && l.IsActive);
    }

    public async Task<bool> ClientExistsAsync(Guid clientId)
    {
        return await _context.Clients.AnyAsync(c => c.Id == clientId && c.IsActive);
    }

    public async Task<Guid> CreateLeadFromClientAsync(Guid clientId, Guid assignedToUserId)
    {
        // Obtener el cliente
        var client = await _context.Clients.FindAsync(clientId);
        if (client == null)
            throw new InvalidOperationException($"Cliente con ID {clientId} no encontrado");

        // Crear el nuevo lead con datos por defecto usando LeadService
        var newLead = new Lead
        {
            Code = "TEMP", // Valor temporal, será reemplazado por LeadService
            ClientId = clientId,
            AssignedToId = assignedToUserId,
            Status = LeadStatus.Registered,
            CaptureSource = LeadCaptureSource.Company, // Por defecto
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
        };

        // Usar LeadService para crear el lead (esto generará el código automáticamente)
        var createdLead = await _leadService.CreateLeadAsync(newLead);

        return createdLead.Id;
    }

    /// <summary>
    /// Genera el contenido HTML del email para el código OTP
    /// </summary>
    private string GenerateOtpEmailContent(
        string supervisorName,
        string otpCode,
        string requesterName
    )
    {
        // Formatea el OTP como "XXX-XXX"
        string formattedOtp =
            otpCode.Length == 6 ? $"{otpCode.Substring(0, 3)}-{otpCode.Substring(3, 3)}" : otpCode;

        var businessName = _businessInfo.Value.Business;
        var businessUrl = _businessInfo.Value.Url;

        return $@"
        <h1 style=""color: #1a1a1a; font-weight: 700; font-size: 28px; margin-bottom: 25px; text-align: center;"">
            Código de Autorización
        </h1>
        
        <p style=""font-size: 16px; color: #1a1a1a; margin-bottom: 20px;"">
            Estimado(a) <span class=""highlight"">{supervisorName}</span>,
        </p>
        
        <div class=""info-box"">
            <p style=""font-size: 15px; color: #1a1a1a; margin-bottom: 15px;"">
                El usuario <span class=""highlight"">{requesterName}</span> ha solicitado autorización para desbloquear un descuento especial en la plataforma <span class=""highlight"">{businessName}</span>.
            </p>
        </div>
        
        <div style=""text-align: center; margin: 40px 0;"">
            <div style=""display: inline-block; background: linear-gradient(135deg, #1a1a1a 0%, #333333 100%); border: 3px solid #ffd700; border-radius: 12px; padding: 30px 50px; box-shadow: 0 8px 25px rgba(255, 215, 0, 0.3);"">
                <div style=""font-size: 42px; font-weight: 800; letter-spacing: 8px; color: #ffd700; font-family: 'Montserrat', 'Segoe UI Mono', monospace; text-shadow: 0 2px 4px rgba(0,0,0,0.3);"">
                    {formattedOtp}
                </div>
            </div>
        </div>
        
        <div class=""info-box"">
            <h3 style=""color: #1a1a1a; font-weight: 600; margin-bottom: 15px;"">
                Información Importante:
            </h3>
            <ul style=""color: #333333; font-size: 14px; margin: 0; padding-left: 20px;"">
                <li style=""margin-bottom: 8px;"">Este código es válido por <span class=""highlight"">5 minutos</span>.</li>
                <li style=""margin-bottom: 8px;"">Solo puede ser utilizado <strong>una vez</strong>.</li>
                <li style=""margin-bottom: 8px;"">No comparta este código con terceros.</li>
                <li style=""margin-bottom: 8px;"">Si usted no solicitó este código, por favor ignore este mensaje.</li>
            </ul>
        </div>
        
        <div class=""divider""></div>
        
        <p style=""font-size: 14px; color: #666666; text-align: center; margin-top: 25px;"">
            Si tiene alguna consulta, comuníquese con el equipo de soporte de <span class=""highlight"">{businessName}</span>.
        </p>
        
        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{businessUrl}"" class=""btn"">Acceder a la Plataforma</a>
        </div>";
    }
}
