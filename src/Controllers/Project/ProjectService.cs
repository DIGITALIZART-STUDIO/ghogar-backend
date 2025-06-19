using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GestionHogar.Dtos;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Services;

public class ProjectService : IProjectService
{
    private readonly DatabaseContext _context;

    public ProjectService(DatabaseContext context)
    {
        _context = context;
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

    public async Task<ProjectDTO?> GetProjectByIdAsync(Guid id)
    {
        var project = await _context
            .Projects.Include(p => p.Blocks)
            .ThenInclude(b => b.Lots)
            .FirstOrDefaultAsync(p => p.Id == id);

        return project != null ? ProjectDTO.FromEntity(project) : null;
    }

    public async Task<ProjectDTO> CreateProjectAsync(ProjectCreateDTO dto)
    {
        // Verificar que no existe un proyecto con el mismo nombre
        var existingProject = await _context.Projects.FirstOrDefaultAsync(p =>
            p.Name.ToLower() == dto.Name.ToLower()
        );

        if (existingProject != null)
            throw new InvalidOperationException(
                $"Ya existe un proyecto con el nombre '{dto.Name}'"
            );

        var project = dto.ToEntity();
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        return ProjectDTO.FromEntity(project);
    }

    public async Task<ProjectDTO?> UpdateProjectAsync(Guid id, ProjectUpdateDTO dto)
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
