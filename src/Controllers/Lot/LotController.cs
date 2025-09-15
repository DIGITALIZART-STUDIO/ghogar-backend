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
public class LotsController : ControllerBase
{
    private readonly ILotService _lotService;
    private readonly ILogger<LotsController> _logger;

    public LotsController(ILotService lotService, ILogger<LotsController> logger)
    {
        _lotService = lotService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LotDTO>>> GetAllLots()
    {
        try
        {
            var lots = await _lotService.GetAllLotsAsync();
            return Ok(lots);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener lotes");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpGet("block/{blockId:guid}")]
    public async Task<ActionResult<IEnumerable<LotDTO>>> GetLotsByBlock(Guid blockId)
    {
        try
        {
            var lots = await _lotService.GetLotsByBlockIdAsync(blockId);
            return Ok(lots);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener lotes del bloque");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpGet("block/{blockId:guid}/paginated")]
    public async Task<ActionResult<PaginatedResponseV2<LotDTO>>> GetLotsByBlockPaginated(
        Guid blockId,
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
                "Obteniendo lotes del bloque {BlockId} paginados, página: {Page}, tamaño: {PageSize}, búsqueda: {Search}, preselectedId: {PreselectedId}",
                blockId,
                page,
                pageSize,
                search ?? "null",
                preselectedId ?? "null"
            );

            var result = await _lotService.GetLotsByBlockIdPaginatedAsync(
                blockId,
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
            _logger.LogError(ex, "Error al obtener lotes del bloque paginados");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpGet("project/{projectId:guid}")]
    public async Task<ActionResult<IEnumerable<LotDTO>>> GetLotsByProject(Guid projectId)
    {
        try
        {
            var lots = await _lotService.GetLotsByProjectIdAsync(projectId);
            return Ok(lots);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener lotes del proyecto");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpGet("available")]
    public async Task<ActionResult<IEnumerable<LotDTO>>> GetAvailableLots()
    {
        try
        {
            var lots = await _lotService.GetAvailableLotsAsync();
            return Ok(lots);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener lotes disponibles");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<LotDTO>>> GetLotsByStatus(LotStatus status)
    {
        try
        {
            var lots = await _lotService.GetLotsByStatusAsync(status);
            return Ok(lots);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener lotes por estado");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LotDTO>> GetLotById(Guid id)
    {
        try
        {
            var lot = await _lotService.GetLotByIdAsync(id);
            if (lot == null)
                return NotFound($"Lote con ID {id} no encontrado");

            return Ok(lot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener lote");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpPost]
    public async Task<ActionResult<LotDTO>> CreateLot(LotCreateDTO dto)
    {
        try
        {
            var lot = await _lotService.CreateLotAsync(dto);
            return CreatedAtAction(nameof(GetLotById), new { id = lot.Id }, lot);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear lote");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<LotDTO>> UpdateLot(Guid id, LotUpdateDTO dto)
    {
        try
        {
            var lot = await _lotService.UpdateLotAsync(id, dto);
            if (lot == null)
                return NotFound($"Lote con ID {id} no encontrado");

            return Ok(lot);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar lote");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<LotDTO>> UpdateLotStatus(Guid id, LotStatusUpdateDTO dto)
    {
        try
        {
            var lot = await _lotService.UpdateLotStatusAsync(id, dto.Status);
            if (lot == null)
                return NotFound($"Lote con ID {id} no encontrado");

            return Ok(lot);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar estado del lote");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteLot(Guid id)
    {
        try
        {
            var result = await _lotService.DeleteLotAsync(id);
            if (!result)
                return NotFound($"Lote con ID {id} no encontrado");

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar lote");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpPut("{id:guid}/activate")]
    public async Task<ActionResult> ActivateLot(Guid id)
    {
        try
        {
            var result = await _lotService.ActivateLotAsync(id);
            if (!result)
                return NotFound($"Lote con ID {id} no encontrado");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al activar lote");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpPut("{id:guid}/deactivate")]
    public async Task<ActionResult> DeactivateLot(Guid id)
    {
        try
        {
            var result = await _lotService.DeactivateLotAsync(id);
            if (!result)
                return NotFound($"Lote con ID {id} no encontrado");

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al desactivar lote");
            return StatusCode(500, "Error interno del servidor");
        }
    }
}
