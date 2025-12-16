using GestionHogar.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class AbstractCrudController<T, CreateDTO, PatchDTO> : ControllerBase
    where T : class, IEntity
    where CreateDTO : class, IMapToEntity<T>
    where PatchDTO : class, IEntityPatcher<T>
{
    protected readonly DatabaseContext _context;
    protected readonly DbSet<T> _dbSet;

    public AbstractCrudController(DatabaseContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    // GET: api/[controller]
    [EndpointSummary("Get all")]
    [HttpGet]
    public virtual async Task<ActionResult<IEnumerable<T>>> GetAll()
    {
        return await _dbSet.ToListAsync();
    }

    // GET: api/[controller]/{id}
    [EndpointSummary("Get one by ID")]
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<ActionResult<T>> GetById(Guid id)
    {
        var entity = await _dbSet.FindAsync(id);
        return entity == null ? NotFound() : Ok(entity);
    }

    // POST: api/[controller]
    [EndpointSummary("Create")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public virtual async Task<ActionResult<T>> Create([FromBody] CreateDTO createDTO)
    {
        var entity = createDTO.MapToEntity();

        _dbSet.Add(entity);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    [EndpointSummary("Partial edit one by id")]
    [HttpPatch("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<IActionResult> Patch(Guid id, [FromBody] PatchDTO patchDto)
    {
        var entity = await _dbSet.FindAsync(id);

        if (entity == null)
        {
            return NotFound();
        }

        patchDto.ApplyPatch(entity);
        await _context.SaveChangesAsync();

        return Ok(patchDto);
    }

    [EndpointSummary("Reactivate by id")]
    [HttpPatch("{id}/reactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<IActionResult> Reactivate(Guid id)
    {
        var entity = await _dbSet.FindAsync(id);
        if (entity == null)
        {
            return NotFound();
        }
        entity.IsActive = true;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE: api/[controller]/{id}
    [EndpointSummary("Deactivate by id")]
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _dbSet.FindAsync(id);
        if (entity == null)
            return NotFound();
        entity.IsActive = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
