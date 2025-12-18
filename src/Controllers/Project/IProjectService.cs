using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GestionHogar.Dtos;
using GestionHogar.Model;
using Microsoft.AspNetCore.Http;

namespace GestionHogar.Services;

public interface IProjectService
{
    Task<IEnumerable<ProjectDTO>> GetAllProjectsAsync();
    Task<IEnumerable<ProjectDTO>> GetActiveProjectsAsync();
    Task<PaginatedResponseV2<ProjectDTO>> GetAllProjectsPaginatedAsync(
        int page,
        int pageSize,
        string? search = null,
        string? orderBy = null,
        string? orderDirection = "asc",
        string? preselectedId = null
    );
    Task<PaginatedResponseV2<ProjectDTO>> GetActiveProjectsPaginatedAsync(
        int page,
        int pageSize,
        string? search = null,
        string? orderBy = null,
        string? orderDirection = "asc",
        string? preselectedId = null
    );
    Task<ProjectDTO?> GetProjectByIdAsync(Guid id);
    Task<ProjectDTO> CreateProjectAsync(ProjectCreateDTO dto, IFormFile? projectImage = null);
    Task<ProjectDTO?> UpdateProjectAsync(
        Guid id,
        ProjectUpdateDTO dto,
        IFormFile? projectImage = null
    );
    Task<bool> DeleteProjectAsync(Guid id);
    Task<bool> ActivateProjectAsync(Guid id);
    Task<bool> DeactivateProjectAsync(Guid id);
    Task<bool> ProjectExistsAsync(Guid id);
}
