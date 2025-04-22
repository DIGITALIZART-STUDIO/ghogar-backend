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

    public async Task<IEnumerable<LeadTask>> GetTasksByLeadIdAsync(Guid leadId)
    {
        return await _context
            .LeadTasks.Where(t => t.LeadId == leadId && t.IsActive)
            .Include(t => t.AssignedTo)
            .Include(t => t.Lead)
            .ThenInclude(l => l.Client)
            .OrderBy(t => t.ScheduledDate)
            .ToListAsync();
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

        task.IsCompleted = true;
        task.CompletedDate = DateTime.UtcNow;
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
