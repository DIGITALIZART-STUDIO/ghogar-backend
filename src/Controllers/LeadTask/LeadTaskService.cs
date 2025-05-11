using GestionHogar.Dtos;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Services;

public class LeadTaskService : ILeadTaskService
{
    private readonly DatabaseContext _context;

    public LeadTaskService(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<LeadTask>> GetAllTasksAsync()
    {
        return await _context
            .LeadTasks.Where(t => t.IsActive)
            .Include(t => t.Lead)
            .Include(t => t.AssignedTo)
            .OrderBy(t => t.ScheduledDate)
            .ToListAsync();
    }

    public async Task<LeadTask?> GetTaskByIdAsync(Guid id)
    {
        return await _context
            .LeadTasks.Include(t => t.Lead)
            .Include(t => t.AssignedTo)
            .FirstOrDefaultAsync(t => t.Id == id && t.IsActive);
    }

    public async Task<LeadTasksResponseDto?> GetTasksByLeadIdAsync(Guid leadId)
    {
        // Primero obtener el lead con su cliente y usuario asignado
        var lead = await _context
            .Leads.Where(l => l.Id == leadId && l.IsActive)
            .Include(l => l.Client)
            .Include(l => l.AssignedTo)
            .FirstOrDefaultAsync();

        if (lead == null)
            return null;

        // Verificar que Lead tiene Client y AssignedTo
        if (lead.Client == null || lead.AssignedTo == null)
            return null;

        // Luego obtener las tareas asociadas a ese lead
        var tasks = await _context
            .LeadTasks.Where(t => t.LeadId == leadId && t.IsActive)
            .Include(t => t.AssignedTo)
            .Include(t => t.Lead) // Incluir el lead para poder acceder a su cliente
            .ThenInclude(l => l.Client) // Incluir el cliente del lead
            .OrderBy(t => t.ScheduledDate)
            .ToListAsync();

        // Mapear a DTOs
        var response = new LeadTasksResponseDto
        {
            Lead = new LeadDTO
            {
                Id = lead.Id,
                ClientId = lead.ClientId.Value,
                Client = new ClientDTO
                {
                    Id = lead.Client.Id,
                    Name = lead.Client.Name,
                    Dni = lead.Client.Dni,
                    Ruc = lead.Client.Ruc,
                    CompanyName = lead.Client.CompanyName,
                    PhoneNumber = lead.Client.PhoneNumber,
                    Email = lead.Client.Email,
                    Address = lead.Client.Address,
                    Type = lead.Client.Type.ToString(),
                    IsActive = lead.Client.IsActive,
                },
                AssignedToId = lead.AssignedToId ?? Guid.Empty,
                AssignedTo = new UserBasicDTO
                {
                    Id = lead.AssignedTo.Id,
                    UserName = lead.AssignedTo.UserName ?? string.Empty,
                    Name = lead.AssignedTo.Name,
                    IsActive = lead.AssignedTo.IsActive,
                },
                Status = lead.Status.ToString(),
                Procedency = lead.Procedency,
                IsActive = lead.IsActive,
            },
            Tasks = tasks
                .Where(t => t.AssignedTo != null)
                .Select(t => new LeadTaskDTO
                {
                    Id = t.Id,
                    LeadId = t.LeadId,
                    Lead =
                        t.Lead != null && t.Lead.Client != null
                            ? new LeadDTO
                            {
                                Id = t.Lead.Id,
                                ClientId = t.Lead.ClientId.Value,
                                Client = new ClientDTO
                                {
                                    Id = t.Lead.Client.Id,
                                    Name = t.Lead.Client.Name,
                                    Dni = t.Lead.Client.Dni,
                                    Ruc = t.Lead.Client.Ruc,
                                    CompanyName = t.Lead.Client.CompanyName,
                                    PhoneNumber = t.Lead.Client.PhoneNumber,
                                    Email = t.Lead.Client.Email,
                                    Address = t.Lead.Client.Address,
                                    Type = t.Lead.Client.Type.ToString(),
                                    IsActive = t.Lead.Client.IsActive,
                                },
                                AssignedToId = t.Lead.AssignedToId.Value,
                                Status = t.Lead.Status.ToString(),
                                Procedency = t.Lead.Procedency,
                                IsActive = t.Lead.IsActive,
                            }
                            : null,
                    AssignedToId = t.AssignedToId,
                    AssignedTo = new UserBasicDTO
                    {
                        Id = t.AssignedTo!.Id,
                        UserName = t.AssignedTo.UserName,
                        Name = t.AssignedTo.Name,
                        IsActive = t.AssignedTo.IsActive,
                    },
                    Description = t.Description,
                    ScheduledDate = t.ScheduledDate,
                    CompletedDate = t.CompletedDate,
                    IsCompleted = t.IsCompleted,
                    Type = t.Type.ToString(),
                    IsActive = t.IsActive,
                })
                .ToList(),
        };

        return response;
    }

    public async Task<IEnumerable<LeadTaskDTO>> GetTasksWithFiltersAsync(
        DateTime from,
        DateTime to,
        Guid? assignedToId = null,
        Guid? leadId = null,
        TaskType? taskType = null,
        bool? isCompleted = null
    )
    {
        // Construir la consulta base con los filtros obligatorios de fecha
        var query = _context
            .LeadTasks.Where(t => t.IsActive)
            .Where(t => t.ScheduledDate >= from && t.ScheduledDate <= to)
            .Include(t => t.AssignedTo)
            .Include(t => t.Lead)
            .ThenInclude(l => l.Client)
            .OrderBy(t => t.ScheduledDate)
            .AsQueryable();

        // Aplicar filtros opcionales si fueron proporcionados
        if (assignedToId.HasValue && assignedToId != Guid.Empty)
        {
            query = query.Where(t => t.AssignedToId == assignedToId);
        }

        if (leadId.HasValue && leadId != Guid.Empty)
        {
            // Filtrar por el ClientId del Lead en lugar de por LeadId
            query = query.Where(t => t.Lead.ClientId == leadId);
        }

        if (taskType.HasValue)
        {
            query = query.Where(t => t.Type == taskType);
        }

        if (isCompleted.HasValue)
        {
            query = query.Where(t => t.IsCompleted == isCompleted);
        }

        // Ejecutar la consulta y convertir a DTOs
        var tasks = await query.ToListAsync();

        // Mapear a DTOs
        var taskDTOs = tasks
            .Where(t => t.AssignedTo != null && t.Lead != null && t.Lead.Client != null)
            .Select(t => new LeadTaskDTO
            {
                Id = t.Id,
                LeadId = t.LeadId,
                Lead = new LeadDTO
                {
                    Id = t.Lead!.Id,
                    ClientId = t.Lead.ClientId.Value,
                    Client = new ClientDTO
                    {
                        Id = t.Lead.Client!.Id,
                        Name = t.Lead.Client.Name,
                        Dni = t.Lead.Client.Dni,
                        Ruc = t.Lead.Client.Ruc,
                        CompanyName = t.Lead.Client.CompanyName,
                        PhoneNumber = t.Lead.Client.PhoneNumber,
                        Email = t.Lead.Client.Email,
                        Address = t.Lead.Client.Address,
                        Type = t.Lead.Client.Type.ToString(),
                        IsActive = t.Lead.Client.IsActive,
                    },
                    AssignedToId = t.Lead.AssignedToId ?? Guid.Empty,
                    Status = t.Lead.Status.ToString(),
                    Procedency = t.Lead.Procedency,
                    IsActive = t.Lead.IsActive,
                },
                AssignedToId = t.AssignedToId,
                AssignedTo = new UserBasicDTO
                {
                    Id = t.AssignedTo!.Id,
                    UserName = t.AssignedTo.UserName ?? string.Empty,
                    Name = t.AssignedTo.Name,
                    IsActive = t.AssignedTo.IsActive,
                },
                Description = t.Description,
                ScheduledDate = t.ScheduledDate,
                CompletedDate = t.CompletedDate,
                IsCompleted = t.IsCompleted,
                Type = t.Type.ToString(),
                IsActive = t.IsActive,
            })
            .ToList();

        return taskDTOs;
    }

    public async Task<IEnumerable<LeadTask>> GetTasksByAssignedToIdAsync(Guid userId)
    {
        return await _context
            .LeadTasks.Where(t => t.AssignedToId == userId && t.IsActive)
            .Include(t => t.Lead)
            .OrderBy(t => t.ScheduledDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<LeadTask>> GetTasksByDateRangeAsync(
        DateTime startDate,
        DateTime endDate
    )
    {
        return await _context
            .LeadTasks.Where(t =>
                t.ScheduledDate >= startDate && t.ScheduledDate <= endDate && t.IsActive
            )
            .Include(t => t.Lead)
            .Include(t => t.AssignedTo)
            .OrderBy(t => t.ScheduledDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<LeadTask>> GetPendingTasksAsync()
    {
        return await _context
            .LeadTasks.Where(t => !t.IsCompleted && t.IsActive)
            .Include(t => t.Lead)
            .Include(t => t.AssignedTo)
            .OrderBy(t => t.ScheduledDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<LeadTask>> GetCompletedTasksAsync()
    {
        return await _context
            .LeadTasks.Where(t => t.IsCompleted && t.IsActive)
            .Include(t => t.Lead)
            .Include(t => t.AssignedTo)
            .OrderByDescending(t => t.CompletedDate)
            .ToListAsync();
    }

    public async Task<LeadTask> CreateTaskAsync(LeadTask task)
    {
        _context.LeadTasks.Add(task);
        await _context.SaveChangesAsync();
        return task;
    }

    public async Task<LeadTask?> UpdateTaskAsync(Guid id, LeadTask updatedTask)
    {
        var task = await _context.LeadTasks.FirstOrDefaultAsync(t => t.Id == id && t.IsActive);
        if (task == null)
            return null;

        task.LeadId = updatedTask.LeadId;
        task.AssignedToId = updatedTask.AssignedToId;
        task.Description = updatedTask.Description;
        task.ScheduledDate = updatedTask.ScheduledDate;
        task.IsCompleted = updatedTask.IsCompleted;
        task.CompletedDate = updatedTask.CompletedDate;
        task.Type = updatedTask.Type;
        task.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return task;
    }

    public async Task<bool> CompleteTaskAsync(Guid id)
    {
        var task = await _context.LeadTasks.FirstOrDefaultAsync(t => t.Id == id && t.IsActive);
        if (task == null)
            return false;

        // Toggle el estado de completado
        task.IsCompleted = !task.IsCompleted;

        // Actualizar la fecha de completado según corresponda
        if (task.IsCompleted)
        {
            task.CompletedDate = DateTime.UtcNow;
        }
        else
        {
            task.CompletedDate = null;
        }

        task.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteTaskAsync(Guid id)
    {
        var task = await _context.LeadTasks.FirstOrDefaultAsync(t => t.Id == id && t.IsActive);
        if (task == null)
            return false;

        task.IsActive = false;
        task.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }
}
