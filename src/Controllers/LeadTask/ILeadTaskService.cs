using GestionHogar.Dtos;
using GestionHogar.Model;

namespace GestionHogar.Services;

public interface ILeadTaskService
{
    Task<IEnumerable<LeadTask>> GetAllTasksAsync();
    Task<LeadTask?> GetTaskByIdAsync(Guid id);
    Task<LeadTasksResponseDto?> GetTasksByLeadIdAsync(Guid leadId);
    Task<IEnumerable<LeadTaskDTO>> GetTasksWithFiltersAsync(
        DateTime from,
        DateTime to,
        Guid? assignedToId = null,
        Guid? leadId = null,
        TaskType? taskType = null,
        bool? isCompleted = null
    );
    Task<IEnumerable<LeadTask>> GetTasksByAssignedToIdAsync(Guid userId);
    Task<IEnumerable<LeadTask>> GetTasksByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<LeadTask>> GetPendingTasksAsync();
    Task<IEnumerable<LeadTask>> GetCompletedTasksAsync();
    Task<LeadTask> CreateTaskAsync(LeadTask task);
    Task<LeadTask?> UpdateTaskAsync(Guid id, LeadTask task);
    Task<bool> CompleteTaskAsync(Guid id);
    Task<bool> DeleteTaskAsync(Guid id);
}
