using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GestionHogar.Dtos;
using GestionHogar.Model;
using GestionHogar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GestionHogar.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BlocksController : ControllerBase
{
    private readonly IBlockService _blockService;
    private readonly ILogger<BlocksController> _logger;

    public BlocksController(IBlockService blockService, ILogger<BlocksController> logger)
    {
        _blockService = blockService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BlockDTO>>> GetAllBlocks()
    {
        try
        {
            var blocks = await _blockService.GetAllBlocksAsync();
            return Ok(blocks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener bloques");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpGet("project/{projectId:guid}")]
    public async Task<ActionResult<IEnumerable<BlockDTO>>> GetBlocksByProject(Guid projectId)
    {
        try
        {
            var blocks = await _blockService.GetBlocksByProjectIdAsync(projectId);
            return Ok(blocks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener bloques del proyecto");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpGet("project/{projectId:guid}/active")]
    public async Task<ActionResult<IEnumerable<BlockDTO>>> GetActiveBlocksByProject(Guid projectId)
    {
        try
        {
            var blocks = await _blockService.GetActiveBlocksByProjectIdAsync(projectId);
            return Ok(blocks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener bloques activos del proyecto");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpGet("project/{projectId:guid}/active/paginated")]
    public async Task<
        ActionResult<PaginatedResponseV2<BlockDTO>>
    > GetActiveBlocksByProjectPaginated(
        Guid projectId,
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
            // Validar parámetros
            if (page < 1)
            {
                return BadRequest("La página debe ser mayor a 0");
            }
            if (pageSize < 1 || pageSize > 100)
            {
                return BadRequest("El tamaño de página debe estar entre 1 y 100");
            }

            _logger.LogInformation(
                "Obteniendo bloques activos del proyecto {ProjectId} paginados, página: {Page}, tamaño: {PageSize}, búsqueda: {Search}, preselectedId: {PreselectedId}",
                projectId,
                page,
                pageSize,
                search ?? "null",
                preselectedId ?? "null"
            );

            var result = await _blockService.GetActiveBlocksByProjectIdPaginatedAsync(
                projectId,
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
            _logger.LogError(ex, "Error al obtener bloques activos del proyecto paginados");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BlockDTO>> GetBlockById(Guid id)
    {
        try
        {
            var block = await _blockService.GetBlockByIdAsync(id);
            if (block == null)
                return NotFound($"Bloque con ID {id} no encontrado");

            return Ok(block);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener bloque");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpPost]
    public async Task<ActionResult<BlockDTO>> CreateBlock(BlockCreateDTO dto)
    {
        try
        {
            var block = await _blockService.CreateBlockAsync(dto);
            return CreatedAtAction(nameof(GetBlockById), new { id = block.Id }, block);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear bloque");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BlockDTO>> UpdateBlock(Guid id, BlockUpdateDTO dto)
    {
        try
        {
            var block = await _blockService.UpdateBlockAsync(id, dto);
            if (block == null)
                return NotFound($"Bloque con ID {id} no encontrado");

            return Ok(block);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar bloque");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteBlock(Guid id)
    {
        try
        {
            var result = await _blockService.DeleteBlockAsync(id);
            if (!result)
                return NotFound($"Bloque con ID {id} no encontrado");

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar bloque");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpPut("{id:guid}/activate")]
    public async Task<ActionResult> ActivateBlock(Guid id)
    {
        try
        {
            var result = await _blockService.ActivateBlockAsync(id);
            if (!result)
                return NotFound($"Bloque con ID {id} no encontrado");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al activar bloque");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpPut("{id:guid}/deactivate")]
    public async Task<ActionResult> DeactivateBlock(Guid id)
    {
        try
        {
            var result = await _blockService.DeactivateBlockAsync(id);
            if (!result)
                return NotFound($"Bloque con ID {id} no encontrado");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al desactivar bloque");
            return StatusCode(500, "Error interno del servidor");
        }
    }
}
