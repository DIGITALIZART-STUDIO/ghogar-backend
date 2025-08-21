using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GestionHogar.Dtos;
using Microsoft.AspNetCore.Http;

namespace GestionHogar.Services;

public interface IProjectService
{
    Task<IEnumerable<ProjectDTO>> GetAllProjectsAsync();
    Task<IEnumerable<ProjectDTO>> GetActiveProjectsAsync();
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
