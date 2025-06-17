using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GestionHogar.Dtos;

namespace GestionHogar.Services;

public interface IProjectService
{
    Task<IEnumerable<ProjectDTO>> GetAllProjectsAsync();
    Task<IEnumerable<ProjectDTO>> GetActiveProjectsAsync();
    Task<ProjectDTO?> GetProjectByIdAsync(Guid id);
    Task<ProjectDTO> CreateProjectAsync(ProjectCreateDTO dto);
    Task<ProjectDTO?> UpdateProjectAsync(Guid id, ProjectUpdateDTO dto);
    Task<bool> DeleteProjectAsync(Guid id);
    Task<bool> ActivateProjectAsync(Guid id);
    Task<bool> DeactivateProjectAsync(Guid id);
    Task<bool> ProjectExistsAsync(Guid id);
}
