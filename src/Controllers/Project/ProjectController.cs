using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GestionHogar.Dtos;
using GestionHogar.Model;
using GestionHogar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    [EndpointSummary("Get all projects with pagination")]
    [EndpointDescription(
        "Retrieves all projects with pagination, search and ordering capabilities"
    )]
    public async Task<ActionResult<PaginatedResponseV2<ProjectDTO>>> GetAllProjects(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? orderBy = null,
        [FromQuery] string? orderDirection = "asc",
        [FromQuery] string? preselectedId = null
    )
    {
        try
        {
            if (page < 1)
                page = 1;
            if (pageSize < 1 || pageSize > 100)
                pageSize = 10;

            var result = await _projectService.GetAllProjectsPaginatedAsync(
                page,
                pageSize,
                search,
                orderBy,
                orderDirection,
                preselectedId
            );
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener proyectos con paginación");
            return StatusCode(
                500,
                new { message = "Error interno del servidor", error = ex.Message }
            );
        }
    }

    [HttpGet("active")]
    [EndpointSummary("Get active projects")]
    [EndpointDescription("Retrieves only active projects in the system")]
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

    [HttpGet("active/paginated")]
    [EndpointSummary("Get active projects with pagination")]
    [EndpointDescription(
        "Retrieves active projects with pagination, search and ordering capabilities"
    )]
    public async Task<ActionResult<PaginatedResponseV2<ProjectDTO>>> GetActiveProjectsPaginated(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? orderBy = null,
        [FromQuery] string? orderDirection = "asc",
        [FromQuery] string? preselectedId = null
    )
    {
        try
        {
            if (page < 1)
                page = 1;
            if (pageSize < 1 || pageSize > 100)
                pageSize = 10;

            var result = await _projectService.GetActiveProjectsPaginatedAsync(
                page,
                pageSize,
                search,
                orderBy,
                orderDirection,
                preselectedId
            );
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener proyectos activos con paginación");
            return StatusCode(
                500,
                new { message = "Error interno del servidor", error = ex.Message }
            );
        }
    }

    [HttpGet("{id:guid}")]
    [EndpointSummary("Get project by ID")]
    [EndpointDescription("Retrieves a specific project by its ID")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
    [EndpointSummary("Create project")]
    [EndpointDescription(
        "Creates a new project with the provided information. Optionally accepts an image file."
    )]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ProjectDTO>> CreateProject(
        [FromForm] ProjectCreateDTO dto,
        [FromForm] IFormFile? projectImage = null
    )
    {
        try
        {
            var project = await _projectService.CreateProjectAsync(dto, projectImage);
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
    [EndpointSummary("Update project")]
    [EndpointDescription(
        "Updates an existing project with the provided information. Optionally accepts an image file."
    )]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ProjectDTO>> UpdateProject(
        Guid id,
        [FromForm] ProjectUpdateDTO dto,
        [FromForm] IFormFile? projectImage = null
    )
    {
        try
        {
            var project = await _projectService.UpdateProjectAsync(id, dto, projectImage);
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
