using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GestionHogar.Dtos;
using GestionHogar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GestionHogar.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(IProjectService projectService, ILogger<ProjectsController> logger)
    {
        _projectService = projectService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProjectDTO>>> GetAllProjects()
    {
        try
        {
            var projects = await _projectService.GetAllProjectsAsync();
            return Ok(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener proyectos");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<ProjectDTO>>> GetActiveProjects()
    {
        try
        {
            var projects = await _projectService.GetActiveProjectsAsync();
            return Ok(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener proyectos activos");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectDTO>> GetProjectById(Guid id)
    {
        try
        {
            var project = await _projectService.GetProjectByIdAsync(id);
            if (project == null)
                return NotFound($"Proyecto con ID {id} no encontrado");

            return Ok(project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener proyecto");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDTO>> CreateProject(ProjectCreateDTO dto)
    {
        try
        {
            var project = await _projectService.CreateProjectAsync(dto);
            return CreatedAtAction(nameof(GetProjectById), new { id = project.Id }, project);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear proyecto");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProjectDTO>> UpdateProject(Guid id, ProjectUpdateDTO dto)
    {
        try
        {
            var project = await _projectService.UpdateProjectAsync(id, dto);
            if (project == null)
                return NotFound($"Proyecto con ID {id} no encontrado");

            return Ok(project);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar proyecto");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteProject(Guid id)
    {
        try
        {
            var result = await _projectService.DeleteProjectAsync(id);
            if (!result)
                return NotFound($"Proyecto con ID {id} no encontrado");

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar proyecto");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpPut("{id:guid}/activate")]
    public async Task<ActionResult> ActivateProject(Guid id)
    {
        try
        {
            var result = await _projectService.ActivateProjectAsync(id);
            if (!result)
                return NotFound($"Proyecto con ID {id} no encontrado");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al activar proyecto");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpPut("{id:guid}/deactivate")]
    public async Task<ActionResult> DeactivateProject(Guid id)
    {
        try
        {
            var result = await _projectService.DeactivateProjectAsync(id);
            if (!result)
                return NotFound($"Proyecto con ID {id} no encontrado");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al desactivar proyecto");
            return StatusCode(500, "Error interno del servidor");
        }
    }
}
