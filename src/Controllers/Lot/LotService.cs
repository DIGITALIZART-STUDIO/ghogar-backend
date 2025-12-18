using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GestionHogar.Dtos;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Services;

public class LotService : ILotService
{
    private readonly DatabaseContext _context;

    public LotService(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<LotDTO>> GetAllLotsAsync()
    {
        var lots = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .OrderBy(l => l.Block.Project.Name)
            .ThenBy(l => l.Block.Name)
            .ThenBy(l => l.LotNumber)
            .ToListAsync();

        return lots.Select(LotDTO.FromEntity);
    }

    public async Task<IEnumerable<LotDTO>> GetLotsByBlockIdAsync(Guid blockId)
    {
        var lots = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .Where(l => l.BlockId == blockId)
            .OrderBy(l => l.LotNumber)
            .ToListAsync();

        return lots.Select(LotDTO.FromEntity);
    }

    public async Task<PaginatedResponseV2<LotDTO>> GetLotsByBlockIdPaginatedAsync(
        Guid blockId,
        int page,
        int pageSize,
        string? search = null,
        string? orderBy = null,
        string? orderDirection = "asc",
        string? preselectedId = null
    )
    {
        var query = _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .Where(l => l.BlockId == blockId);

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
                // En la primera página: incluir el lote preseleccionado al inicio
                var preselectedLot = await _context
                    .Lots.Include(l => l.Block)
                    .ThenInclude(b => b.Project)
                    .FirstOrDefaultAsync(l => l.Id == preselectedGuid && l.BlockId == blockId);

                if (preselectedLot != null)
                {
                    // Modificar la query para que el lote preseleccionado aparezca primero
                    query = query.OrderBy(l => l.Id == preselectedGuid ? 0 : 1);
                }
            }
            else
            {
                // En páginas siguientes: excluir el lote preseleccionado para evitar duplicados
                query = query.Where(l => l.Id != preselectedGuid);
            }
        }

        // Aplicar filtro de búsqueda si se proporciona
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.ToLower();
            query = query.Where(l =>
                (l.LotNumber != null && l.LotNumber.ToLower().Contains(searchTerm))
                || (
                    l.Block != null
                    && l.Block.Name != null
                    && l.Block.Name.ToLower().Contains(searchTerm)
                )
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
                    "lotnumber" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(l => l.LotNumber)
                        : query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenBy(l => l.LotNumber),
                    "area" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(l => l.Area)
                        : query.OrderBy(l => l.Id == preselectedGuid ? 0 : 1).ThenBy(l => l.Area),
                    "price" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(l => l.Price)
                        : query.OrderBy(l => l.Id == preselectedGuid ? 0 : 1).ThenBy(l => l.Price),
                    "status" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(l => l.Status)
                        : query.OrderBy(l => l.Id == preselectedGuid ? 0 : 1).ThenBy(l => l.Status),
                    "createdat" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(l => l.CreatedAt)
                        : query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenBy(l => l.CreatedAt),
                    _ => query
                        .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                        .ThenBy(l => l.LotNumber),
                };
            }
            else
            {
                query = orderBy.ToLower() switch
                {
                    "lotnumber" => isDescending
                        ? query.OrderByDescending(l => l.LotNumber)
                        : query.OrderBy(l => l.LotNumber),
                    "area" => isDescending
                        ? query.OrderByDescending(l => l.Area)
                        : query.OrderBy(l => l.Area),
                    "price" => isDescending
                        ? query.OrderByDescending(l => l.Price)
                        : query.OrderBy(l => l.Price),
                    "status" => isDescending
                        ? query.OrderByDescending(l => l.Status)
                        : query.OrderBy(l => l.Status),
                    "createdat" => isDescending
                        ? query.OrderByDescending(l => l.CreatedAt)
                        : query.OrderBy(l => l.CreatedAt),
                    _ => query.OrderBy(l => l.LotNumber), // Ordenamiento por defecto
                };
            }
        }
        else
        {
            // Ordenamiento por defecto
            if (preselectedGuid.HasValue && page == 1)
            {
                query = query
                    .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                    .ThenBy(l => l.LotNumber);
            }
            else
            {
                query = query.OrderBy(l => l.LotNumber);
            }
        }

        // Ejecutar paginación
        var totalCount = await query.CountAsync();
        var lots = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        // Convertir a DTOs usando el método FromEntity existente
        var items = lots.Select(LotDTO.FromEntity).ToList();

        return PaginatedResponseV2<LotDTO>.Create(items, totalCount, page, pageSize);
    }

    public async Task<IEnumerable<LotDTO>> GetLotsByProjectIdAsync(Guid projectId)
    {
        var lots = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .Where(l => l.Block.ProjectId == projectId)
            .OrderBy(l => l.Block.Name)
            .ThenBy(l => l.LotNumber)
            .ToListAsync();

        return lots.Select(LotDTO.FromEntity);
    }

    public async Task<PaginatedResponseV2<LotDTO>> GetLotsByProjectOrBlockAsync(
        Guid? projectId = null,
        Guid? blockId = null,
        int page = 1,
        int pageSize = 10,
        string? search = null,
        string? orderBy = null,
        string? orderDirection = "asc",
        string? preselectedId = null,
        string? status = null
    )
    {
        // Validar que al menos uno de los parámetros esté presente
        if (!projectId.HasValue && !blockId.HasValue)
        {
            throw new ArgumentException("Debe proporcionar al menos projectId o blockId");
        }

        IQueryable<Lot> query = _context.Lots.Include(l => l.Block).ThenInclude(b => b.Project);

        // Aplicar filtro según el parámetro proporcionado
        if (projectId.HasValue && blockId.HasValue)
        {
            // Si ambos están presentes, filtrar por ambos
            query = query.Where(l =>
                l.Block.ProjectId == projectId.Value && l.BlockId == blockId.Value
            );
        }
        else if (projectId.HasValue)
        {
            // Solo filtrar por proyecto
            query = query.Where(l => l.Block.ProjectId == projectId.Value);
        }
        else if (blockId.HasValue)
        {
            // Solo filtrar por bloque
            query = query.Where(l => l.BlockId == blockId.Value);
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
                // En la primera página: incluir el lote preseleccionado al inicio
                var preselectedLot = await _context
                    .Lots.Include(l => l.Block)
                    .ThenInclude(b => b.Project)
                    .FirstOrDefaultAsync(l =>
                        l.Id == preselectedGuid
                        && (projectId.HasValue ? l.Block.ProjectId == projectId.Value : true)
                        && (blockId.HasValue ? l.BlockId == blockId.Value : true)
                    );

                if (preselectedLot != null)
                {
                    // Modificar la query para que el lote preseleccionado aparezca primero
                    query = query.OrderBy(l => l.Id == preselectedGuid ? 0 : 1);
                }
            }
            else
            {
                // En páginas siguientes: excluir el lote preseleccionado para evitar duplicados
                query = query.Where(l => l.Id != preselectedGuid);
            }
        }

        // Aplicar filtro de búsqueda si se proporciona
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.ToLower();
            query = query.Where(l =>
                (l.LotNumber != null && l.LotNumber.ToLower().Contains(searchTerm))
                || (
                    l.Block != null
                    && l.Block.Name != null
                    && l.Block.Name.ToLower().Contains(searchTerm)
                )
                || (
                    l.Block != null
                    && l.Block.Project != null
                    && l.Block.Project.Name != null
                    && l.Block.Project.Name.ToLower().Contains(searchTerm)
                )
            );
        }

        // Aplicar filtro por estado si se proporciona
        if (
            !string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<LotStatus>(status, true, out var lotStatus)
        )
        {
            query = query.Where(l => l.Status == lotStatus);
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
                    "lotnumber" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(l => l.LotNumber)
                        : query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenBy(l => l.LotNumber),
                    "area" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(l => l.Area)
                        : query.OrderBy(l => l.Id == preselectedGuid ? 0 : 1).ThenBy(l => l.Area),
                    "price" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(l => l.Price)
                        : query.OrderBy(l => l.Id == preselectedGuid ? 0 : 1).ThenBy(l => l.Price),
                    "status" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(l => l.Status)
                        : query.OrderBy(l => l.Id == preselectedGuid ? 0 : 1).ThenBy(l => l.Status),
                    "createdat" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(l => l.CreatedAt)
                        : query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenBy(l => l.CreatedAt),
                    "blockname" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(l => l.Block.Name.ToLower())
                        : query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenBy(l => l.Block.Name.ToLower()),
                    "projectname" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(l => l.Block.Project.Name.ToLower())
                        : query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenBy(l => l.Block.Project.Name.ToLower()),
                    _ => query
                        .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                        .ThenBy(l => l.LotNumber),
                };
            }
            else
            {
                query = orderBy.ToLower() switch
                {
                    "lotnumber" => isDescending
                        ? query.OrderByDescending(l => l.LotNumber)
                        : query.OrderBy(l => l.LotNumber),
                    "area" => isDescending
                        ? query.OrderByDescending(l => l.Area)
                        : query.OrderBy(l => l.Area),
                    "price" => isDescending
                        ? query.OrderByDescending(l => l.Price)
                        : query.OrderBy(l => l.Price),
                    "status" => isDescending
                        ? query.OrderByDescending(l => l.Status)
                        : query.OrderBy(l => l.Status),
                    "createdat" => isDescending
                        ? query.OrderByDescending(l => l.CreatedAt)
                        : query.OrderBy(l => l.CreatedAt),
                    "blockname" => isDescending
                        ? query.OrderByDescending(l => l.Block.Name.ToLower())
                        : query.OrderBy(l => l.Block.Name.ToLower()),
                    "projectname" => isDescending
                        ? query.OrderByDescending(l => l.Block.Project.Name.ToLower())
                        : query.OrderBy(l => l.Block.Project.Name.ToLower()),
                    _ => query.OrderBy(l => l.LotNumber), // Ordenamiento por defecto
                };
            }
        }
        else
        {
            // Ordenamiento por defecto
            if (preselectedGuid.HasValue && page == 1)
            {
                query = query
                    .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                    .ThenBy(l => l.LotNumber);
            }
            else
            {
                query = query.OrderBy(l => l.LotNumber);
            }
        }

        // Ejecutar paginación
        var totalCount = await query.CountAsync();
        var lots = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        // Convertir a DTOs usando el método FromEntity existente
        var items = lots.Select(LotDTO.FromEntity).ToList();

        return PaginatedResponseV2<LotDTO>.Create(items, totalCount, page, pageSize);
    }

    public async Task<IEnumerable<LotDTO>> GetLotsByStatusAsync(LotStatus status)
    {
        var lots = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .Where(l => l.Status == status)
            .OrderBy(l => l.Block.Project.Name)
            .ThenBy(l => l.Block.Name)
            .ThenBy(l => l.LotNumber)
            .ToListAsync();

        return lots.Select(LotDTO.FromEntity);
    }

    public async Task<IEnumerable<LotDTO>> GetAvailableLotsAsync()
    {
        return await GetLotsByStatusAsync(LotStatus.Available);
    }

    public async Task<LotDTO?> GetLotByIdAsync(Guid id)
    {
        var lot = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .FirstOrDefaultAsync(l => l.Id == id);

        return lot != null ? LotDTO.FromEntity(lot) : null;
    }

    public async Task<LotDTO> CreateLotAsync(LotCreateDTO dto)
    {
        // Verificar que el bloque existe y está activo
        var block = await _context
            .Blocks.Include(b => b.Project)
            .FirstOrDefaultAsync(b => b.Id == dto.BlockId);

        if (block == null)
            throw new InvalidOperationException($"Bloque con ID {dto.BlockId} no encontrado");

        if (!block.IsActive)
            throw new InvalidOperationException("No se puede crear un lote en un bloque inactivo");

        if (!block.Project.IsActive)
            throw new InvalidOperationException(
                "No se puede crear un lote en un proyecto inactivo"
            );

        // Verificar que no existe un lote con el mismo número en el bloque
        var existingLot = await _context.Lots.FirstOrDefaultAsync(l =>
            l.BlockId == dto.BlockId && l.LotNumber.ToLower() == dto.LotNumber.ToLower()
        );

        if (existingLot != null)
            throw new InvalidOperationException(
                $"Ya existe un lote con el número '{dto.LotNumber}' en este bloque"
            );

        var lot = dto.ToEntity();
        _context.Lots.Add(lot);
        await _context.SaveChangesAsync();

        // Recargar el lote con sus relaciones
        var createdLot = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .FirstAsync(l => l.Id == lot.Id);

        return LotDTO.FromEntity(createdLot);
    }

    public async Task<LotDTO?> UpdateLotAsync(Guid id, LotUpdateDTO dto)
    {
        var lot = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lot == null)
            return null;

        // Si se está cambiando el número, verificar que no exista otro lote con ese número en el bloque
        if (
            !string.IsNullOrWhiteSpace(dto.LotNumber)
            && dto.LotNumber.ToLower() != lot.LotNumber.ToLower()
        )
        {
            var existingLot = await _context.Lots.FirstOrDefaultAsync(l =>
                l.BlockId == lot.BlockId
                && l.LotNumber.ToLower() == dto.LotNumber.ToLower()
                && l.Id != id
            );

            if (existingLot != null)
                throw new InvalidOperationException(
                    $"Ya existe un lote con el número '{dto.LotNumber}' en este bloque"
                );
        }

        // Validar cambio de estado si se especifica
        if (dto.Status.HasValue && dto.Status.Value != lot.Status)
        {
            if (!await CanChangeLotStatusAsync(id, dto.Status.Value))
                throw new InvalidOperationException(
                    $"No se puede cambiar el estado del lote de {lot.Status} a {dto.Status.Value}"
                );
        }

        dto.ApplyTo(lot);
        await _context.SaveChangesAsync();

        // Recargar el lote con sus relaciones
        var updatedLot = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .FirstAsync(l => l.Id == id);

        return LotDTO.FromEntity(updatedLot);
    }

    public async Task<LotDTO?> UpdateLotStatusAsync(Guid id, LotStatus status)
    {
        var lot = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lot == null)
            return null;

        if (!await CanChangeLotStatusAsync(id, status))
            throw new InvalidOperationException(
                $"No se puede cambiar el estado del lote de {lot.Status} a {status}"
            );

        lot.Status = status;
        lot.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return LotDTO.FromEntity(lot);
    }

    public async Task<bool> DeleteLotAsync(Guid id)
    {
        var lot = await _context.Lots.FindAsync(id);
        if (lot == null)
            return false;

        // No se puede eliminar un lote reservado o vendido
        if (lot.Status == LotStatus.Reserved || lot.Status == LotStatus.Sold)
            throw new InvalidOperationException("No se puede eliminar un lote reservado o vendido");

        // TODO: Verificar que no tenga cotizaciones activas
        // var hasActiveQuotations = await _context.Quotations
        //     .AnyAsync(q => q.LotId == id && q.Status == QuotationStatus.Active);
        // if (hasActiveQuotations)
        //     throw new InvalidOperationException("No se puede eliminar un lote con cotizaciones activas");

        _context.Lots.Remove(lot);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ActivateLotAsync(Guid id)
    {
        var lot = await _context.Lots.FindAsync(id);
        if (lot == null)
            return false;

        lot.IsActive = true;
        lot.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeactivateLotAsync(Guid id)
    {
        var lot = await _context.Lots.FindAsync(id);
        if (lot == null)
            return false;

        // No se puede desactivar un lote reservado o vendido
        if (lot.Status == LotStatus.Reserved || lot.Status == LotStatus.Sold)
            throw new InvalidOperationException(
                "No se puede desactivar un lote reservado o vendido"
            );

        lot.IsActive = false;
        lot.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> LotExistsAsync(Guid id)
    {
        return await _context.Lots.AnyAsync(l => l.Id == id);
    }

    public async Task<bool> LotExistsInBlockAsync(Guid blockId, string lotNumber)
    {
        return await _context.Lots.AnyAsync(l =>
            l.BlockId == blockId && l.LotNumber.ToLower() == lotNumber.ToLower()
        );
    }

    public async Task<bool> CanChangeLotStatusAsync(Guid id, LotStatus newStatus)
    {
        var lot = await _context.Lots.FindAsync(id);
        if (lot == null)
            return false;

        var currentStatus = lot.Status;

        // Reglas de negocio para cambio de estado
        return (currentStatus, newStatus) switch
        {
            // Desde Available se puede ir a cualquier estado
            (LotStatus.Available, _) => true,

            // Desde Quoted se puede volver a Available o avanzar a Reserved
            (LotStatus.Quoted, LotStatus.Available) => true,
            (LotStatus.Quoted, LotStatus.Reserved) => true,

            // Desde Reserved se puede volver a Available o avanzar a Sold
            (LotStatus.Reserved, LotStatus.Available) => true,
            (LotStatus.Reserved, LotStatus.Sold) => true,

            // Desde Sold no se puede cambiar a ningún estado
            (LotStatus.Sold, _) => false,

            // Cualquier otro cambio no permitido
            _ => false,
        };
    }
}

public class LotService : ILotService
{
    private readonly DatabaseContext _context;

    public LotService(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<LotDTO>> GetAllLotsAsync()
    {
        var lots = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .OrderBy(l => l.Block.Project.Name)
            .ThenBy(l => l.Block.Name)
            .ThenBy(l => l.LotNumber)
            .ToListAsync();

        return lots.Select(LotDTO.FromEntity);
    }

    public async Task<IEnumerable<LotDTO>> GetLotsByBlockIdAsync(Guid blockId)
    {
        var lots = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .Where(l => l.BlockId == blockId)
            .OrderBy(l => l.LotNumber)
            .ToListAsync();

        return lots.Select(LotDTO.FromEntity);
    }

    public async Task<PaginatedResponseV2<LotDTO>> GetLotsByBlockIdPaginatedAsync(
        Guid blockId,
        int page,
        int pageSize,
        string? search = null,
        string? orderBy = null,
        string? orderDirection = "asc",
        string? preselectedId = null
    )
    {
        var query = _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .Where(l => l.BlockId == blockId);

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
                // En la primera página: incluir el lote preseleccionado al inicio
                var preselectedLot = await _context
                    .Lots.Include(l => l.Block)
                    .ThenInclude(b => b.Project)
                    .FirstOrDefaultAsync(l => l.Id == preselectedGuid && l.BlockId == blockId);

                if (preselectedLot != null)
                {
                    // Modificar la query para que el lote preseleccionado aparezca primero
                    query = query.OrderBy(l => l.Id == preselectedGuid ? 0 : 1);
                }
            }
            else
            {
                // En páginas siguientes: excluir el lote preseleccionado para evitar duplicados
                query = query.Where(l => l.Id != preselectedGuid);
            }
        }

        // Aplicar filtro de búsqueda si se proporciona
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.ToLower();
            query = query.Where(l =>
                (l.LotNumber != null && l.LotNumber.ToLower().Contains(searchTerm))
                || (
                    l.Block != null
                    && l.Block.Name != null
                    && l.Block.Name.ToLower().Contains(searchTerm)
                )
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
                    "lotnumber" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(l => l.LotNumber)
                        : query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenBy(l => l.LotNumber),
                    "area" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(l => l.Area)
                        : query.OrderBy(l => l.Id == preselectedGuid ? 0 : 1).ThenBy(l => l.Area),
                    "price" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(l => l.Price)
                        : query.OrderBy(l => l.Id == preselectedGuid ? 0 : 1).ThenBy(l => l.Price),
                    "status" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(l => l.Status)
                        : query.OrderBy(l => l.Id == preselectedGuid ? 0 : 1).ThenBy(l => l.Status),
                    "createdat" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(l => l.CreatedAt)
                        : query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenBy(l => l.CreatedAt),
                    _ => query
                        .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                        .ThenBy(l => l.LotNumber),
                };
            }
            else
            {
                query = orderBy.ToLower() switch
                {
                    "lotnumber" => isDescending
                        ? query.OrderByDescending(l => l.LotNumber)
                        : query.OrderBy(l => l.LotNumber),
                    "area" => isDescending
                        ? query.OrderByDescending(l => l.Area)
                        : query.OrderBy(l => l.Area),
                    "price" => isDescending
                        ? query.OrderByDescending(l => l.Price)
                        : query.OrderBy(l => l.Price),
                    "status" => isDescending
                        ? query.OrderByDescending(l => l.Status)
                        : query.OrderBy(l => l.Status),
                    "createdat" => isDescending
                        ? query.OrderByDescending(l => l.CreatedAt)
                        : query.OrderBy(l => l.CreatedAt),
                    _ => query.OrderBy(l => l.LotNumber), // Ordenamiento por defecto
                };
            }
        }
        else
        {
            // Ordenamiento por defecto
            if (preselectedGuid.HasValue && page == 1)
            {
                query = query
                    .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                    .ThenBy(l => l.LotNumber);
            }
            else
            {
                query = query.OrderBy(l => l.LotNumber);
            }
        }

        // Ejecutar paginación
        var totalCount = await query.CountAsync();
        var lots = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        // Convertir a DTOs usando el método FromEntity existente
        var items = lots.Select(LotDTO.FromEntity).ToList();

        return PaginatedResponseV2<LotDTO>.Create(items, totalCount, page, pageSize);
    }

    public async Task<IEnumerable<LotDTO>> GetLotsByProjectIdAsync(Guid projectId)
    {
        var lots = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .Where(l => l.Block.ProjectId == projectId)
            .OrderBy(l => l.Block.Name)
            .ThenBy(l => l.LotNumber)
            .ToListAsync();

        return lots.Select(LotDTO.FromEntity);
    }

    public async Task<PaginatedResponseV2<LotDTO>> GetLotsByProjectOrBlockAsync(
        Guid? projectId = null,
        Guid? blockId = null,
        int page = 1,
        int pageSize = 10,
        string? search = null,
        string? orderBy = null,
        string? orderDirection = "asc",
        string? preselectedId = null,
        string? status = null
    )
    {
        // Validar que al menos uno de los parámetros esté presente
        if (!projectId.HasValue && !blockId.HasValue)
        {
            throw new ArgumentException("Debe proporcionar al menos projectId o blockId");
        }

        IQueryable<Lot> query = _context.Lots.Include(l => l.Block).ThenInclude(b => b.Project);

        // Aplicar filtro según el parámetro proporcionado
        if (projectId.HasValue && blockId.HasValue)
        {
            // Si ambos están presentes, filtrar por ambos
            query = query.Where(l =>
                l.Block.ProjectId == projectId.Value && l.BlockId == blockId.Value
            );
        }
        else if (projectId.HasValue)
        {
            // Solo filtrar por proyecto
            query = query.Where(l => l.Block.ProjectId == projectId.Value);
        }
        else if (blockId.HasValue)
        {
            // Solo filtrar por bloque
            query = query.Where(l => l.BlockId == blockId.Value);
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
                // En la primera página: incluir el lote preseleccionado al inicio
                var preselectedLot = await _context
                    .Lots.Include(l => l.Block)
                    .ThenInclude(b => b.Project)
                    .FirstOrDefaultAsync(l =>
                        l.Id == preselectedGuid
                        && (projectId.HasValue ? l.Block.ProjectId == projectId.Value : true)
                        && (blockId.HasValue ? l.BlockId == blockId.Value : true)
                    );

                if (preselectedLot != null)
                {
                    // Modificar la query para que el lote preseleccionado aparezca primero
                    query = query.OrderBy(l => l.Id == preselectedGuid ? 0 : 1);
                }
            }
            else
            {
                // En páginas siguientes: excluir el lote preseleccionado para evitar duplicados
                query = query.Where(l => l.Id != preselectedGuid);
            }
        }

        // Aplicar filtro de búsqueda si se proporciona
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.ToLower();
            query = query.Where(l =>
                (l.LotNumber != null && l.LotNumber.ToLower().Contains(searchTerm))
                || (
                    l.Block != null
                    && l.Block.Name != null
                    && l.Block.Name.ToLower().Contains(searchTerm)
                )
                || (
                    l.Block != null
                    && l.Block.Project != null
                    && l.Block.Project.Name != null
                    && l.Block.Project.Name.ToLower().Contains(searchTerm)
                )
            );
        }

        // Aplicar filtro por estado si se proporciona
        if (
            !string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<LotStatus>(status, true, out var lotStatus)
        )
        {
            query = query.Where(l => l.Status == lotStatus);
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
                    "lotnumber" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(l => l.LotNumber)
                        : query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenBy(l => l.LotNumber),
                    "area" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(l => l.Area)
                        : query.OrderBy(l => l.Id == preselectedGuid ? 0 : 1).ThenBy(l => l.Area),
                    "price" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(l => l.Price)
                        : query.OrderBy(l => l.Id == preselectedGuid ? 0 : 1).ThenBy(l => l.Price),
                    "status" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(l => l.Status)
                        : query.OrderBy(l => l.Id == preselectedGuid ? 0 : 1).ThenBy(l => l.Status),
                    "createdat" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(l => l.CreatedAt)
                        : query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenBy(l => l.CreatedAt),
                    "blockname" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(l => l.Block.Name.ToLower())
                        : query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenBy(l => l.Block.Name.ToLower()),
                    "projectname" => isDescending
                        ? query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(l => l.Block.Project.Name.ToLower())
                        : query
                            .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                            .ThenBy(l => l.Block.Project.Name.ToLower()),
                    _ => query
                        .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                        .ThenBy(l => l.LotNumber),
                };
            }
            else
            {
                query = orderBy.ToLower() switch
                {
                    "lotnumber" => isDescending
                        ? query.OrderByDescending(l => l.LotNumber)
                        : query.OrderBy(l => l.LotNumber),
                    "area" => isDescending
                        ? query.OrderByDescending(l => l.Area)
                        : query.OrderBy(l => l.Area),
                    "price" => isDescending
                        ? query.OrderByDescending(l => l.Price)
                        : query.OrderBy(l => l.Price),
                    "status" => isDescending
                        ? query.OrderByDescending(l => l.Status)
                        : query.OrderBy(l => l.Status),
                    "createdat" => isDescending
                        ? query.OrderByDescending(l => l.CreatedAt)
                        : query.OrderBy(l => l.CreatedAt),
                    "blockname" => isDescending
                        ? query.OrderByDescending(l => l.Block.Name.ToLower())
                        : query.OrderBy(l => l.Block.Name.ToLower()),
                    "projectname" => isDescending
                        ? query.OrderByDescending(l => l.Block.Project.Name.ToLower())
                        : query.OrderBy(l => l.Block.Project.Name.ToLower()),
                    _ => query.OrderBy(l => l.LotNumber), // Ordenamiento por defecto
                };
            }
        }
        else
        {
            // Ordenamiento por defecto
            if (preselectedGuid.HasValue && page == 1)
            {
                query = query
                    .OrderBy(l => l.Id == preselectedGuid ? 0 : 1)
                    .ThenBy(l => l.LotNumber);
            }
            else
            {
                query = query.OrderBy(l => l.LotNumber);
            }
        }

        // Ejecutar paginación
        var totalCount = await query.CountAsync();
        var lots = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        // Convertir a DTOs usando el método FromEntity existente
        var items = lots.Select(LotDTO.FromEntity).ToList();

        return PaginatedResponseV2<LotDTO>.Create(items, totalCount, page, pageSize);
    }

    public async Task<IEnumerable<LotDTO>> GetLotsByStatusAsync(LotStatus status)
    {
        var lots = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .Where(l => l.Status == status)
            .OrderBy(l => l.Block.Project.Name)
            .ThenBy(l => l.Block.Name)
            .ThenBy(l => l.LotNumber)
            .ToListAsync();

        return lots.Select(LotDTO.FromEntity);
    }

    public async Task<IEnumerable<LotDTO>> GetAvailableLotsAsync()
    {
        return await GetLotsByStatusAsync(LotStatus.Available);
    }

    public async Task<LotDTO?> GetLotByIdAsync(Guid id)
    {
        var lot = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .FirstOrDefaultAsync(l => l.Id == id);

        return lot != null ? LotDTO.FromEntity(lot) : null;
    }

    public async Task<LotDTO> CreateLotAsync(LotCreateDTO dto)
    {
        // Verificar que el bloque existe y está activo
        var block = await _context
            .Blocks.Include(b => b.Project)
            .FirstOrDefaultAsync(b => b.Id == dto.BlockId);

        if (block == null)
            throw new InvalidOperationException($"Bloque con ID {dto.BlockId} no encontrado");

        if (!block.IsActive)
            throw new InvalidOperationException("No se puede crear un lote en un bloque inactivo");

        if (!block.Project.IsActive)
            throw new InvalidOperationException(
                "No se puede crear un lote en un proyecto inactivo"
            );

        // Verificar que no existe un lote con el mismo número en el bloque
        var existingLot = await _context.Lots.FirstOrDefaultAsync(l =>
            l.BlockId == dto.BlockId && l.LotNumber.ToLower() == dto.LotNumber.ToLower()
        );

        if (existingLot != null)
            throw new InvalidOperationException(
                $"Ya existe un lote con el número '{dto.LotNumber}' en este bloque"
            );

        var lot = dto.ToEntity();
        _context.Lots.Add(lot);
        await _context.SaveChangesAsync();

        // Recargar el lote con sus relaciones
        var createdLot = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .FirstAsync(l => l.Id == lot.Id);

        return LotDTO.FromEntity(createdLot);
    }

    public async Task<LotDTO?> UpdateLotAsync(Guid id, LotUpdateDTO dto)
    {
        var lot = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lot == null)
            return null;

        // Determinar el bloque objetivo (nuevo o actual)
        var targetBlockId = dto.BlockId ?? lot.BlockId;
        var targetLotNumber = dto.LotNumber?.Trim() ?? lot.LotNumber;

        // Si se está cambiando el bloque, validar el nuevo bloque
        if (dto.BlockId.HasValue && dto.BlockId.Value != lot.BlockId)
        {
            var newBlock = await _context
                .Blocks.Include(b => b.Project)
                .FirstOrDefaultAsync(b => b.Id == dto.BlockId.Value);

            if (newBlock == null)
                throw new InvalidOperationException(
                    $"Bloque con ID {dto.BlockId.Value} no encontrado"
                );

            if (!newBlock.IsActive)
                throw new InvalidOperationException(
                    "No se puede mover un lote a un bloque inactivo"
                );

            if (!newBlock.Project.IsActive)
                throw new InvalidOperationException(
                    "No se puede mover un lote a un proyecto inactivo"
                );
        }

        // Verificar que no exista otro lote con el mismo número en el bloque objetivo
        // (ya sea porque se cambió el número, el bloque, o ambos)
        if (
            (dto.LotNumber != null && dto.LotNumber.ToLower() != lot.LotNumber.ToLower())
            || (dto.BlockId.HasValue && dto.BlockId.Value != lot.BlockId)
        )
        {
            var existingLot = await _context.Lots.FirstOrDefaultAsync(l =>
                l.BlockId == targetBlockId
                && l.LotNumber.ToLower() == targetLotNumber.ToLower()
                && l.Id != id
            );

            if (existingLot != null)
                throw new InvalidOperationException(
                    $"Ya existe un lote con el número '{targetLotNumber}' en este bloque"
                );
        }

        // Validar cambio de estado si se especifica
        if (dto.Status.HasValue && dto.Status.Value != lot.Status)
        {
            if (!await CanChangeLotStatusAsync(id, dto.Status.Value))
                throw new InvalidOperationException(
                    $"No se puede cambiar el estado del lote de {lot.Status} a {dto.Status.Value}"
                );
        }

        dto.ApplyTo(lot);
        await _context.SaveChangesAsync();

        // Recargar el lote con sus relaciones
        var updatedLot = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .FirstAsync(l => l.Id == id);

        return LotDTO.FromEntity(updatedLot);
    }

    public async Task<LotDTO?> UpdateLotStatusAsync(Guid id, LotStatus status)
    {
        var lot = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lot == null)
            return null;

        if (!await CanChangeLotStatusAsync(id, status))
            throw new InvalidOperationException(
                $"No se puede cambiar el estado del lote de {lot.Status} a {status}"
            );

        lot.Status = status;
        lot.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return LotDTO.FromEntity(lot);
    }

    public async Task<bool> DeleteLotAsync(Guid id)
    {
        var lot = await _context.Lots.FindAsync(id);
        if (lot == null)
            return false;

        // No se puede eliminar un lote reservado o vendido
        if (lot.Status == LotStatus.Reserved || lot.Status == LotStatus.Sold)
            throw new InvalidOperationException("No se puede eliminar un lote reservado o vendido");

        // TODO: Verificar que no tenga cotizaciones activas
        // var hasActiveQuotations = await _context.Quotations
        //     .AnyAsync(q => q.LotId == id && q.Status == QuotationStatus.Active);
        // if (hasActiveQuotations)
        //     throw new InvalidOperationException("No se puede eliminar un lote con cotizaciones activas");

        _context.Lots.Remove(lot);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ActivateLotAsync(Guid id)
    {
        var lot = await _context.Lots.FindAsync(id);
        if (lot == null)
            return false;

        lot.IsActive = true;
        lot.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeactivateLotAsync(Guid id)
    {
        var lot = await _context.Lots.FindAsync(id);
        if (lot == null)
            return false;

        // No se puede desactivar un lote reservado o vendido
        if (lot.Status == LotStatus.Reserved || lot.Status == LotStatus.Sold)
            throw new InvalidOperationException(
                "No se puede desactivar un lote reservado o vendido"
            );

        lot.IsActive = false;
        lot.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> LotExistsAsync(Guid id)
    {
        return await _context.Lots.AnyAsync(l => l.Id == id);
    }

    public async Task<bool> LotExistsInBlockAsync(Guid blockId, string lotNumber)
    {
        return await _context.Lots.AnyAsync(l =>
            l.BlockId == blockId && l.LotNumber.ToLower() == lotNumber.ToLower()
        );
    }

    public async Task<bool> CanChangeLotStatusAsync(Guid id, LotStatus newStatus)
    {
        var lot = await _context.Lots.FindAsync(id);
        if (lot == null)
            return false;

        var currentStatus = lot.Status;

        // Reglas de negocio para cambio de estado
        return (currentStatus, newStatus) switch
        {
            // Desde Available se puede ir a cualquier estado
            (LotStatus.Available, _) => true,

            // Desde Quoted se puede volver a Available o avanzar a Reserved
            (LotStatus.Quoted, LotStatus.Available) => true,
            (LotStatus.Quoted, LotStatus.Reserved) => true,

            // Desde Reserved se puede volver a Available o avanzar a Sold
            (LotStatus.Reserved, LotStatus.Available) => true,
            (LotStatus.Reserved, LotStatus.Sold) => true,

            // Desde Sold no se puede cambiar a ningún estado
            (LotStatus.Sold, _) => false,

            // Cualquier otro cambio no permitido
            _ => false,
        };
    }
}
