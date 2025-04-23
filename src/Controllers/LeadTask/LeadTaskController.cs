using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using GestionHogar.Controllers.Dtos;
using GestionHogar.Dtos;
using GestionHogar.Model;
using GestionHogar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionHogar.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class LeadTasksController : ControllerBase
{
    private readonly ILeadTaskService _taskService;
    private static readonly TimeZoneInfo peruTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
        "SA Pacific Standard Time"
    ); // Lima, Perú (UTC-5)

    public LeadTasksController(ILeadTaskService taskService)
    {
        _taskService = taskService;
    }

    // Función helper para parsear fechas en diferentes formatos
    private bool TryParseDate(string dateString, out DateTime result)
    {
        // Intentar parsear como ISO 8601 completo (2025-04-17T12:00:00)
        if (
            DateTime.TryParse(
                dateString,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result
            )
        )
        {
            // Convertir a UTC si la fecha viene con otra zona horaria
            if (result.Kind != DateTimeKind.Utc)
            {
                result = DateTime.SpecifyKind(result, DateTimeKind.Unspecified);
                result = TimeZoneInfo.ConvertTimeToUtc(result, peruTimeZone);
            }
            return true;
        }

        // Intentar parsear como formato simple (yyyy-MM-dd)
        if (
            DateTime.TryParseExact(
                dateString,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result
            )
        )
        {
            // Para fechas sin hora, usar mediodía en hora peruana (17:00 UTC)
            result = new DateTime(
                result.Year,
                result.Month,
                result.Day,
                17,
                0,
                0,
                DateTimeKind.Utc
            );
            return true;
        }

        return false;
    }

    // GET: api/leadtasks
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LeadTask>>> GetTasks()
    {
        var tasks = await _taskService.GetAllTasksAsync();
        return Ok(tasks);
    }

    // GET: api/leadtasks/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<LeadTask>> GetTask(Guid id)
    {
        var task = await _taskService.GetTaskByIdAsync(id);
        if (task == null)
            return NotFound();

        return Ok(task);
    }

    // GET: api/leadtasks/lead/{leadId}
    [HttpGet("lead/{leadId}")]
    public async Task<ActionResult<LeadTasksResponseDto>> GetTasksByLead(Guid leadId)
    {
        var tasksResponse = await _taskService.GetTasksByLeadIdAsync(leadId);

        if (tasksResponse == null)
            return NotFound();

        return Ok(tasksResponse);
    }

    // GET: api/leadtasks/user/{userId}
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<LeadTask>>> GetTasksByUser(Guid userId)
    {
        var tasks = await _taskService.GetTasksByAssignedToIdAsync(userId);
        return Ok(tasks);
    }

    // GET: api/leadtasks/pending
    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<LeadTask>>> GetPendingTasks()
    {
        var tasks = await _taskService.GetPendingTasksAsync();
        return Ok(tasks);
    }

    // GET: api/leadtasks/completed
    [HttpGet("completed")]
    public async Task<ActionResult<IEnumerable<LeadTask>>> GetCompletedTasks()
    {
        var tasks = await _taskService.GetCompletedTasksAsync();
        return Ok(tasks);
    }

    // GET: api/leadtasks/daterange
    [HttpGet("daterange")]
    public async Task<ActionResult<IEnumerable<LeadTask>>> GetTasksByDateRange(
        [FromQuery] string startDate,
        [FromQuery] string endDate
    )
    {
        if (
            !TryParseDate(startDate, out DateTime start) || !TryParseDate(endDate, out DateTime end)
        )
        {
            return BadRequest(
                "Formato de fecha inválido. Use yyyy-MM-dd o formato ISO (ej: 2025-04-17T12:00:00)."
            );
        }

        // Asegurar que incluya todo el día final
        end = end.AddDays(1).AddSeconds(-1);

        var tasks = await _taskService.GetTasksByDateRangeAsync(start, end);
        return Ok(tasks);
    }

    // POST: api/leadtasks
    [HttpPost]
    public async Task<ActionResult<LeadTask>> CreateTask(LeadTaskCreateDto taskDto)
    {
        try
        {
            if (!TryParseDate(taskDto.ScheduledDate, out DateTime scheduledDate))
            {
                return BadRequest(
                    "Formato de fecha inválido. Use yyyy-MM-dd o formato ISO (ej: 2025-04-17T12:00:00)."
                );
            }

            var task = new LeadTask
            {
                LeadId = taskDto.LeadId,
                AssignedToId = taskDto.AssignedToId,
                Description = taskDto.Description,
                ScheduledDate = scheduledDate,
                Type = taskDto.Type,
            };

            var createdTask = await _taskService.CreateTaskAsync(task);
            return CreatedAtAction(nameof(GetTask), new { id = createdTask.Id }, createdTask);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // PUT: api/leadtasks/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult<LeadTask>> UpdateTask(Guid id, LeadTaskUpdateDto taskDto)
    {
        try
        {
            var existingTask = await _taskService.GetTaskByIdAsync(id);
            if (existingTask == null)
                return NotFound();

            // Actualiza solo los campos que no son nulos
            if (taskDto.LeadId.HasValue)
                existingTask.LeadId = taskDto.LeadId.Value;

            if (taskDto.AssignedToId.HasValue)
                existingTask.AssignedToId = taskDto.AssignedToId.Value;

            if (taskDto.Description != null)
                existingTask.Description = taskDto.Description;

            if (taskDto.ScheduledDate != null)
            {
                if (!TryParseDate(taskDto.ScheduledDate, out DateTime scheduledDate))
                {
                    return BadRequest(
                        "Formato de fecha inválido. Use yyyy-MM-dd o formato ISO (ej: 2025-04-17T12:00:00)."
                    );
                }

                existingTask.ScheduledDate = scheduledDate;
            }

            if (taskDto.Type.HasValue)
                existingTask.Type = taskDto.Type.Value;

            if (taskDto.IsCompleted.HasValue)
            {
                existingTask.IsCompleted = taskDto.IsCompleted.Value;
                if (taskDto.IsCompleted.Value && !existingTask.CompletedDate.HasValue)
                    existingTask.CompletedDate = DateTime.UtcNow;
                else if (!taskDto.IsCompleted.Value)
                    existingTask.CompletedDate = null;
            }

            var updatedTask = await _taskService.UpdateTaskAsync(id, existingTask);
            return Ok(updatedTask);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // POST: api/leadtasks/{id}/complete
    [HttpPost("{id}/complete")]
    public async Task<ActionResult> CompleteTask(Guid id)
    {
        var success = await _taskService.CompleteTaskAsync(id);
        if (!success)
            return NotFound();

        return NoContent();
    }

    // DELETE: api/leadtasks/{id}
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteTask(Guid id)
    {
        var success = await _taskService.DeleteTaskAsync(id);
        if (!success)
            return NotFound();

        return NoContent();
    }
}
