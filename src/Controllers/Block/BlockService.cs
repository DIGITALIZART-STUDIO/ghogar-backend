using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GestionHogar.Dtos;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Services;

public class BlockService : IBlockService
{
    private readonly DatabaseContext _context;

    public BlockService(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<BlockDTO>> GetAllBlocksAsync()
    {
        var blocks = await _context
            .Blocks.Include(b => b.Project)
            .Include(b => b.Lots)
            .OrderBy(b => b.Project.Name)
            .ThenBy(b => b.Name)
            .ToListAsync();

        return blocks.Select(BlockDTO.FromEntity);
    }

    public async Task<PaginatedResponseV2<BlockDTO>> GetBlocksByProjectIdAsync(
        Guid projectId,
        int page = 1,
        int pageSize = 10,
        string? search = null,
        string? orderBy = null,
        string? orderDirection = "asc",
        string? preselectedId = null
    )
    {
        var query = _context
            .Blocks.Include(b => b.Project)
            .Include(b => b.Lots)
            .Where(b => b.ProjectId == projectId);

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
                // En la primera página: incluir el bloque preseleccionado al inicio
                var preselectedBlock = await _context
                    .Blocks.Include(b => b.Project)
                    .Include(b => b.Lots)
                    .FirstOrDefaultAsync(b => b.Id == preselectedGuid && b.ProjectId == projectId);

                if (preselectedBlock != null)
                {
                    // Modificar la query para que el bloque preseleccionado aparezca primero
                    query = query.OrderBy(b => b.Id == preselectedGuid ? 0 : 1);
                }
            }
            else
            {
                // En páginas siguientes: excluir el bloque preseleccionado para evitar duplicados
                query = query.Where(b => b.Id != preselectedGuid);
            }
        }

        // Aplicar filtro de búsqueda si se proporciona
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.ToLower();
            query = query.Where(b => (b.Name != null && b.Name.ToLower().Contains(searchTerm)));
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
                            .OrderBy(b => b.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(b => b.Name)
                        : query.OrderBy(b => b.Id == preselectedGuid ? 0 : 1).ThenBy(b => b.Name),
                    "description" => isDescending
                        ? query
                            .OrderBy(b => b.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(b => b.Name)
                        : query.OrderBy(b => b.Id == preselectedGuid ? 0 : 1).ThenBy(b => b.Name),
                    "createdat" => isDescending
                        ? query
                            .OrderBy(b => b.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(b => b.CreatedAt)
                        : query
                            .OrderBy(b => b.Id == preselectedGuid ? 0 : 1)
                            .ThenBy(b => b.CreatedAt),
                    _ => query.OrderBy(b => b.Id == preselectedGuid ? 0 : 1).ThenBy(b => b.Name),
                };
            }
            else
            {
                query = orderBy.ToLower() switch
                {
                    "name" => isDescending
                        ? query.OrderByDescending(b => b.Name.ToLower())
                        : query.OrderByDescending(b => b.CreatedAt),
                    "description" => isDescending
                        ? query.OrderByDescending(b => b.Name.ToLower())
                        : query.OrderByDescending(b => b.CreatedAt),
                    "createdat" => isDescending
                        ? query.OrderByDescending(b => b.CreatedAt)
                        : query.OrderBy(b => b.CreatedAt),
                    _ => query.OrderByDescending(b => b.CreatedAt), // Ordenamiento por defecto
                };
            }
        }
        else
        {
            // Ordenamiento por defecto
            if (preselectedGuid.HasValue && page == 1)
            {
                query = query.OrderBy(b => b.Id == preselectedGuid ? 0 : 1).ThenBy(b => b.Name);
            }
            else
            {
                query = query.OrderByDescending(b => b.CreatedAt);
            }
        }

        // Ejecutar paginación
        var totalCount = await query.CountAsync();
        var blocks = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        // Convertir a DTOs usando el método FromEntity existente
        var items = blocks.Select(BlockDTO.FromEntity).ToList();

        return PaginatedResponseV2<BlockDTO>.Create(items, totalCount, page, pageSize);
    }

    public async Task<IEnumerable<BlockDTO>> GetActiveBlocksByProjectIdAsync(Guid projectId)
    {
        var blocks = await _context
            .Blocks.Include(b => b.Project)
            .Include(b => b.Lots)
            .Where(b => b.ProjectId == projectId && b.IsActive)
            .OrderBy(b => b.Name)
            .ToListAsync();

        return blocks.Select(BlockDTO.FromEntity);
    }

    public async Task<PaginatedResponseV2<BlockDTO>> GetActiveBlocksByProjectIdPaginatedAsync(
        Guid projectId,
        int page,
        int pageSize,
        string? search = null,
        string? orderBy = null,
        string? orderDirection = "asc",
        string? preselectedId = null
    )
    {
        var query = _context
            .Blocks.Include(b => b.Project)
            .Include(b => b.Lots)
            .Where(b => b.ProjectId == projectId && b.IsActive);

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
                // En la primera página: incluir el bloque preseleccionado al inicio
                var preselectedBlock = await _context
                    .Blocks.Include(b => b.Project)
                    .Include(b => b.Lots)
                    .FirstOrDefaultAsync(b =>
                        b.Id == preselectedGuid && b.ProjectId == projectId && b.IsActive
                    );

                if (preselectedBlock != null)
                {
                    // Modificar la query para que el bloque preseleccionado aparezca primero
                    query = query.OrderBy(b => b.Id == preselectedGuid ? 0 : 1);
                }
            }
            else
            {
                // En páginas siguientes: excluir el bloque preseleccionado para evitar duplicados
                query = query.Where(b => b.Id != preselectedGuid);
            }
        }

        // Aplicar filtro de búsqueda si se proporciona
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.ToLower();
            query = query.Where(b => (b.Name != null && b.Name.ToLower().Contains(searchTerm)));
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
                            .OrderBy(b => b.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(b => b.Name)
                        : query.OrderBy(b => b.Id == preselectedGuid ? 0 : 1).ThenBy(b => b.Name),
                    "description" => isDescending
                        ? query
                            .OrderBy(b => b.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(b => b.Name)
                        : query.OrderBy(b => b.Id == preselectedGuid ? 0 : 1).ThenBy(b => b.Name),
                    "createdat" => isDescending
                        ? query
                            .OrderBy(b => b.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(b => b.CreatedAt)
                        : query
                            .OrderBy(b => b.Id == preselectedGuid ? 0 : 1)
                            .ThenBy(b => b.CreatedAt),
                    _ => query.OrderBy(b => b.Id == preselectedGuid ? 0 : 1).ThenBy(b => b.Name),
                };
            }
            else
            {
                query = orderBy.ToLower() switch
                {
                    "name" => isDescending
                        ? query.OrderByDescending(b => b.Name.ToLower())
                        : query.OrderByDescending(b => b.CreatedAt),
                    "description" => isDescending
                        ? query.OrderByDescending(b => b.Name.ToLower())
                        : query.OrderByDescending(b => b.CreatedAt),
                    "createdat" => isDescending
                        ? query.OrderByDescending(b => b.CreatedAt)
                        : query.OrderBy(b => b.CreatedAt),
                    _ => query.OrderByDescending(b => b.CreatedAt), // Ordenamiento por defecto
                };
            }
        }
        else
        {
            // Ordenamiento por defecto
            if (preselectedGuid.HasValue && page == 1)
            {
                query = query.OrderBy(b => b.Id == preselectedGuid ? 0 : 1).ThenBy(b => b.Name);
            }
            else
            {
                query = query.OrderByDescending(b => b.CreatedAt);
            }
        }

        // Ejecutar paginación
        var totalCount = await query.CountAsync();
        var blocks = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        // Convertir a DTOs usando el método FromEntity existente
        var items = blocks.Select(BlockDTO.FromEntity).ToList();

        return PaginatedResponseV2<BlockDTO>.Create(items, totalCount, page, pageSize);
    }

    public async Task<BlockDTO?> GetBlockByIdAsync(Guid id)
    {
        var block = await _context
            .Blocks.Include(b => b.Project)
            .Include(b => b.Lots)
            .FirstOrDefaultAsync(b => b.Id == id);

        return block != null ? BlockDTO.FromEntity(block) : null;
    }

    public async Task<BlockDTO> CreateBlockAsync(BlockCreateDTO dto)
    {
        // Verificar que el proyecto existe y está activo
        var project = await _context.Projects.FindAsync(dto.ProjectId);
        if (project == null)
            throw new InvalidOperationException($"Proyecto con ID {dto.ProjectId} no encontrado");

        if (!project.IsActive)
            throw new InvalidOperationException(
                "No se puede crear un bloque en un proyecto inactivo"
            );

        // Verificar que no existe un bloque con el mismo nombre en el proyecto
        var existingBlock = await _context.Blocks.FirstOrDefaultAsync(b =>
            b.ProjectId == dto.ProjectId && b.Name.ToLower() == dto.Name.ToLower()
        );

        if (existingBlock != null)
            throw new InvalidOperationException(
                $"Ya existe un bloque con el nombre '{dto.Name}' en este proyecto"
            );

        var block = dto.ToEntity();
        _context.Blocks.Add(block);
        await _context.SaveChangesAsync();

        // Recargar el bloque con sus relaciones
        var createdBlock = await _context
            .Blocks.Include(b => b.Project)
            .Include(b => b.Lots)
            .FirstAsync(b => b.Id == block.Id);

        return BlockDTO.FromEntity(createdBlock);
    }

    public async Task<BlockDTO?> UpdateBlockAsync(Guid id, BlockUpdateDTO dto)
    {
        var block = await _context
            .Blocks.Include(b => b.Project)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (block == null)
            return null;

        // Si se está cambiando el nombre, verificar que no exista otro bloque con ese nombre en el proyecto
        if (!string.IsNullOrWhiteSpace(dto.Name) && dto.Name.ToLower() != block.Name.ToLower())
        {
            var existingBlock = await _context.Blocks.FirstOrDefaultAsync(b =>
                b.ProjectId == block.ProjectId
                && b.Name.ToLower() == dto.Name.ToLower()
                && b.Id != id
            );

            if (existingBlock != null)
                throw new InvalidOperationException(
                    $"Ya existe un bloque con el nombre '{dto.Name}' en este proyecto"
                );
        }

        dto.ApplyTo(block);
        await _context.SaveChangesAsync();

        // Recargar el bloque con sus relaciones
        var updatedBlock = await _context
            .Blocks.Include(b => b.Project)
            .Include(b => b.Lots)
            .FirstAsync(b => b.Id == id);

        return BlockDTO.FromEntity(updatedBlock);
    }

    public async Task<bool> DeleteBlockAsync(Guid id)
    {
        var block = await _context.Blocks.Include(b => b.Lots).FirstOrDefaultAsync(b => b.Id == id);

        if (block == null)
            return false;

        // Verificar que no hay lotes vendidos o reservados
        var hasReservedOrSoldLots = block.Lots.Any(l =>
            l.Status == LotStatus.Reserved || l.Status == LotStatus.Sold
        );

        if (hasReservedOrSoldLots)
            throw new InvalidOperationException(
                "No se puede eliminar un bloque que tiene lotes reservados o vendidos"
            );

        _context.Blocks.Remove(block);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ActivateBlockAsync(Guid id)
    {
        var block = await _context.Blocks.FindAsync(id);
        if (block == null)
            return false;

        block.IsActive = true;
        block.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeactivateBlockAsync(Guid id)
    {
        var block = await _context.Blocks.FindAsync(id);
        if (block == null)
            return false;

        block.IsActive = false;
        block.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> BlockExistsAsync(Guid id)
    {
        return await _context.Blocks.AnyAsync(b => b.Id == id);
    }

    public async Task<bool> BlockExistsInProjectAsync(Guid projectId, string name)
    {
        return await _context.Blocks.AnyAsync(b =>
            b.ProjectId == projectId && b.Name.ToLower() == name.ToLower()
        );
    }
}
