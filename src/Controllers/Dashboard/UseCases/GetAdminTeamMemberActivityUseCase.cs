using GestionHogar.Controllers;
using GestionHogar.Model;
using GestionHogar.Utils;
using Microsoft.EntityFrameworkCore;

public class GetAdminTeamMemberActivityUseCase
{
    private readonly DatabaseContext _db;

    public GetAdminTeamMemberActivityUseCase(DatabaseContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Devuelve actividad reciente de un SalesAdvisor. Null si el usuario no existe o no es SalesAdvisor.
    /// </summary>
    public async Task<AdminTeamMemberActivityDto?> ExecuteAsync(
        Guid userId,
        DateOnly? from = null,
        DateOnly? to = null,
        int? year = null
    )
    {
        var yearToUse = year ?? DateTime.UtcNow.Year;
        var (rangeStart, rangeEnd) = ResolveEffectiveRange(yearToUse, from, to);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return null;

        var isSalesAdvisor = await (
            from ur in _db.UserRoles
            join r in _db.Roles on ur.RoleId equals r.Id
            where ur.UserId == userId && r.Name == "SalesAdvisor"
            select ur.UserId
        ).AnyAsync();

        if (!isSalesAdvisor)
            return null;

        var leadEntities = await _db
            .Leads.Include(l => l.Client)
            .Include(l => l.AssignedTo)
            .Include(l => l.Project)
            .Where(l =>
                l.AssignedToId == userId && l.EntryDate >= rangeStart && l.EntryDate < rangeEnd
            )
            .OrderByDescending(l => l.EntryDate)
            .Take(5)
            .ToListAsync();

        var leads = leadEntities
            .Select(l => new RecentLeadDto
            {
                Id = l.Id,
                ClientName = l.Client?.Name ?? "Sin cliente",
                ClientPhone = l.Client?.PhoneNumber ?? "",
                CaptureSource = l.CaptureSource.ToString(),
                Status = l.Status.ToString(),
                DaysUntilExpiration = LeadExpirationHelper.GetDaysUntilExpiration(
                    LeadExpirationHelper.GetReferenceDate(
                        l.EntryDate,
                        l.CreatedAt,
                        l.LastRecycledAt
                    )
                ),
                AssignedTo = l.AssignedTo?.Name,
                ProjectName = l.Project?.Name ?? "Sin proyecto",
                EntryDate = l.EntryDate,
                Priority = GetPriorityForLead(l),
            })
            .ToList();

        var tasks = await _db
            .LeadTasks.Where(t =>
                t.AssignedToId == userId
                && t.IsActive
                && t.CreatedAt >= rangeStart
                && t.CreatedAt < rangeEnd
            )
            .OrderByDescending(t => t.CreatedAt)
            .Take(5)
            .Select(t => new AdminTeamMemberTaskDto
            {
                Id = t.Id,
                LeadId = t.LeadId,
                Description = t.Description,
                Type = t.Type.ToString(),
                IsCompleted = t.IsCompleted,
                ScheduledDate = t.ScheduledDate,
                CompletedDate = t.CompletedDate,
                CreatedAt = t.CreatedAt,
            })
            .ToListAsync();

        return new AdminTeamMemberActivityDto
        {
            UserId = user.Id,
            UserName = user.Name,
            Leads = leads,
            Tasks = tasks,
        };
    }

    private static (DateTime RangeStart, DateTime RangeEndExclusive) ResolveEffectiveRange(
        int yearToUse,
        DateOnly? from,
        DateOnly? to
    )
    {
        if (from.HasValue && to.HasValue)
        {
            var rangeStart = from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var rangeEnd = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            return (rangeStart, rangeEnd);
        }

        var yearStart = new DateTime(yearToUse, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearEnd = new DateTime(yearToUse + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return (yearStart, yearEnd);
    }

    private static string GetPriorityForLead(Lead lead)
    {
        var referenceDate = LeadExpirationHelper.GetReferenceDate(
            lead.EntryDate,
            lead.CreatedAt,
            lead.LastRecycledAt
        );
        var daysUntilExpiration = LeadExpirationHelper.GetDaysUntilExpiration(referenceDate);

        if (daysUntilExpiration <= 1 || !lead.AssignedToId.HasValue)
            return "high";
        if (daysUntilExpiration <= 3)
            return "medium";
        return "low";
    }
}
