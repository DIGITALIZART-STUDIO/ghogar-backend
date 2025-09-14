using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GestionHogar.Dtos;
using GestionHogar.Model;
using GestionHogar.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Services;

public class ProjectService : IProjectService
{
    private readonly DatabaseContext _context;
    private readonly ICloudflareService _cloudflareService;

    public ProjectService(DatabaseContext context, ICloudflareService cloudflareService)
    {
        _context = context;
        _cloudflareService = cloudflareService;
    }

    public async Task<IEnumerable<ProjectDTO>> GetAllProjectsAsync()
    {
        var projects = await _context
            .Projects.Include(p => p.Blocks)
            .ThenInclude(b => b.Lots)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return projects.Select(ProjectDTO.FromEntity);
    }

    public async Task<IEnumerable<ProjectDTO>> GetActiveProjectsAsync()
    {
        var projects = await _context
            .Projects.Include(p => p.Blocks)
            .ThenInclude(b => b.Lots)
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return projects.Select(ProjectDTO.FromEntity);
    }

    public async Task<PaginatedResponseV2<ProjectDTO>> GetActiveProjectsPaginatedAsync(
        int page,
        int pageSize,
        string? search = null,
        string? orderBy = null,
        string? orderDirection = "asc",
        string? preselectedId = null
    )
    {
        var query = _context
            .Projects.Include(p => p.Blocks)
            .ThenInclude(b => b.Lots)
            .Where(p => p.IsActive);

        // Lógica para preselectedId - incluir en la query base
        Guid? preselectedGuid = null;
        if (
            !string.IsNullOrWhiteSpace(preselectedId)
            && Guid.TryParse(preselectedId, out var parsedGuid)
        )
        {
            preselectedGuid = parsedGuid;
            // Si hay un preselectedId, modificar la query para incluirlo en la primera página
            if (page == 1)
            {
                // Crear una query que incluya el proyecto preseleccionado al inicio
                var preselectedProject = await _context
                    .Projects.Include(p => p.Blocks)
                    .ThenInclude(b => b.Lots)
                    .FirstOrDefaultAsync(p => p.Id == preselectedGuid && p.IsActive);

                if (preselectedProject != null)
                {
                    // Modificar la query para que el proyecto preseleccionado aparezca primero
                    query = query.OrderBy(p => p.Id == preselectedGuid ? 0 : 1);
                }
            }
        }

        // Aplicar filtro de búsqueda si se proporciona
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.ToLower();
            query = query.Where(p =>
                (p.Name != null && p.Name.ToLower().Contains(searchTerm))
                || (p.Location != null && p.Location.ToLower().Contains(searchTerm))
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
                            .OrderBy(p => p.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(p => p.Name)
                        : query.OrderBy(p => p.Id == preselectedGuid ? 0 : 1).ThenBy(p => p.Name),
                    "location" => isDescending
                        ? query
                            .OrderBy(p => p.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(p => p.Location)
                        : query
                            .OrderBy(p => p.Id == preselectedGuid ? 0 : 1)
                            .ThenBy(p => p.Location),
                    "createdat" => isDescending
                        ? query
                            .OrderBy(p => p.Id == preselectedGuid ? 0 : 1)
                            .ThenByDescending(p => p.CreatedAt)
                        : query
                            .OrderBy(p => p.Id == preselectedGuid ? 0 : 1)
                            .ThenBy(p => p.CreatedAt),
                    _ => query
                        .OrderBy(p => p.Id == preselectedGuid ? 0 : 1)
                        .ThenByDescending(p => p.CreatedAt),
                };
            }
            else
            {
                query = orderBy.ToLower() switch
                {
                    "name" => isDescending
                        ? query.OrderByDescending(p => p.Name)
                        : query.OrderBy(p => p.Name),
                    "location" => isDescending
                        ? query.OrderByDescending(p => p.Location)
                        : query.OrderBy(p => p.Location),
                    "createdat" => isDescending
                        ? query.OrderByDescending(p => p.CreatedAt)
                        : query.OrderBy(p => p.CreatedAt),
                    _ => query.OrderByDescending(p => p.CreatedAt), // Ordenamiento por defecto
                };
            }
        }
        else
        {
            // Ordenamiento por defecto
            if (preselectedGuid.HasValue && page == 1)
            {
                query = query
                    .OrderBy(p => p.Id == preselectedGuid ? 0 : 1)
                    .ThenByDescending(p => p.CreatedAt);
            }
            else
            {
                query = query.OrderByDescending(p => p.CreatedAt);
            }
        }

        // Ejecutar paginación
        var totalCount = await query.CountAsync();
        var projects = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        // Convertir a DTOs usando el método FromEntity existente
        var items = projects.Select(ProjectDTO.FromEntity).ToList();

        return PaginatedResponseV2<ProjectDTO>.Create(items, totalCount, page, pageSize);
    }

    public async Task<ProjectDTO?> GetProjectByIdAsync(Guid id)
    {
        var project = await _context
            .Projects.Include(p => p.Blocks)
            .ThenInclude(b => b.Lots)
            .FirstOrDefaultAsync(p => p.Id == id);

        return project != null ? ProjectDTO.FromEntity(project) : null;
    }

    public async Task<ProjectDTO> CreateProjectAsync(
        ProjectCreateDTO dto,
        IFormFile? projectImage = null
    )
    {
        // Verificar que no existe un proyecto con el mismo nombre
        var existingProject = await _context.Projects.FirstOrDefaultAsync(p =>
            p.Name.ToLower() == dto.Name.ToLower()
        );

        if (existingProject != null)
            throw new InvalidOperationException(
                $"Ya existe un proyecto con el nombre '{dto.Name}'"
            );

        string? projectUrlImage = null;

        // Subir imagen si se proporciona
        if (projectImage != null)
        {
            try
            {
                projectUrlImage = await _cloudflareService.UploadProjectImageAsync(
                    projectImage,
                    dto.Name
                );
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error al subir la imagen: {ex.Message}");
            }
        }

        var project = dto.ToEntity(projectUrlImage);
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        return ProjectDTO.FromEntity(project);
    }

    public async Task<ProjectDTO?> UpdateProjectAsync(
        Guid id,
        ProjectUpdateDTO dto,
        IFormFile? projectImage = null
    )
    {
        var project = await _context.Projects.FindAsync(id);
        if (project == null)
            return null;

        // Si se está cambiando el nombre, verificar que no exista otro proyecto con ese nombre
        if (!string.IsNullOrWhiteSpace(dto.Name) && dto.Name.ToLower() != project.Name.ToLower())
        {
            var existingProject = await _context.Projects.FirstOrDefaultAsync(p =>
                p.Name.ToLower() == dto.Name.ToLower() && p.Id != id
            );

            if (existingProject != null)
                throw new InvalidOperationException(
                    $"Ya existe un proyecto con el nombre '{dto.Name}'"
                );
        }

        // Manejar la actualización de la imagen si se proporciona
        if (projectImage != null)
        {
            try
            {
                var projectName = dto.Name ?? project.Name;
                project.ProjectUrlImage = await _cloudflareService.UpdateProjectImageAsync(
                    projectImage,
                    projectName,
                    project.ProjectUrlImage
                );
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error al actualizar la imagen: {ex.Message}");
            }
        }

        dto.ApplyTo(project);
        await _context.SaveChangesAsync();

        // Recargar el proyecto con sus relaciones
        var updatedProject = await _context
            .Projects.Include(p => p.Blocks)
            .ThenInclude(b => b.Lots)
            .FirstAsync(p => p.Id == id);

        return ProjectDTO.FromEntity(updatedProject);
    }

    public async Task<bool> DeleteProjectAsync(Guid id)
    {
        var project = await _context
            .Projects.Include(p => p.Blocks)
            .ThenInclude(b => b.Lots)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project == null)
            return false;

        // Verificar que no hay lotes vendidos o reservados
        var hasReservedOrSoldLots = project
            .Blocks.SelectMany(b => b.Lots)
            .Any(l => l.Status == LotStatus.Reserved || l.Status == LotStatus.Sold);

        if (hasReservedOrSoldLots)
            throw new InvalidOperationException(
                "No se puede eliminar un proyecto que tiene lotes reservados o vendidos"
            );

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ActivateProjectAsync(Guid id)
    {
        var project = await _context.Projects.FindAsync(id);
        if (project == null)
            return false;

        project.IsActive = true;
        project.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeactivateProjectAsync(Guid id)
    {
        var project = await _context.Projects.FindAsync(id);
        if (project == null)
            return false;

        project.IsActive = false;
        project.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ProjectExistsAsync(Guid id)
    {
        return await _context.Projects.AnyAsync(p => p.Id == id);
    }
}
