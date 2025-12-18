// DTOs para el dashboard de supervisores
namespace GestionHogar.Controllers;

// KPIs principales de leads
public class SupervisorLeadsKpiDto
{
    public int TotalLeads { get; set; }
    public int RegisteredLeads { get; set; }
    public int AttendedLeads { get; set; }
    public int InFollowUpLeads { get; set; }
    public int CompletedLeads { get; set; }
    public int CanceledLeads { get; set; }
    public int ExpiredLeads { get; set; }
    public int UnassignedLeads { get; set; }
}

// Métricas generales del equipo
public class TeamMetricsDto
{
    public int QuotationsGenerated { get; set; }
    public int ReservationsActive { get; set; }
    public int TasksToday { get; set; }
    public double AvgConversionRate { get; set; }
}

// Datos de cada asesor
public class AdvisorPerformanceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int LeadsAssigned { get; set; }
    public int LeadsInFollowUp { get; set; }
    public int LeadsCompleted { get; set; }
    public int QuotationsIssued { get; set; }
    public int ReservationsGenerated { get; set; }
    public int TasksCompleted { get; set; }
    public int TasksPending { get; set; }
    public double AvgResponseTime { get; set; }
    public DateTime? LastActivity { get; set; }
    public double Efficiency { get; set; } // (leadsCompleted / leadsAssigned) * 100
}

// Leads recientes sin asignar o prioritarios
public class RecentLeadDto
{
    public Guid Id { get; set; }
    public string ClientName { get; set; } = "";
    public string ClientPhone { get; set; } = "";
    public string CaptureSource { get; set; } = "";
    public string Status { get; set; } = "";
    public int DaysUntilExpiration { get; set; }
    public string? AssignedTo { get; set; }
    public string ProjectName { get; set; } = "";
    public DateTime EntryDate { get; set; }
    public string Priority { get; set; } = "";
}

// Embudo de conversión
public class ConversionFunnelDto
{
    public string Stage { get; set; } = "";
    public int Count { get; set; }
    public double Percentage { get; set; }
}

// Fuentes de captación
public class LeadSourceDistributionDto
{
    public string Source { get; set; } = "";
    public int Count { get; set; }
    public double Percentage { get; set; }
    public string Color { get; set; } = "";
}

// Actividad semanal
public class WeeklyActivityDto
{
    public string Day { get; set; } = "";
    public int NewLeads { get; set; }
    public int Assigned { get; set; }
    public int Attended { get; set; }
    public int Completed { get; set; }
    public int Expired { get; set; }
}

// Análisis de tareas
public class TaskAnalysisDto
{
    public string Type { get; set; } = "";
    public int Scheduled { get; set; }
    public int Completed { get; set; }
    public int Pending { get; set; }
    public int Overdue { get; set; }
}

// Análisis por proyecto
public class ProjectLeadsAnalysisDto
{
    public string Project { get; set; } = "";
    public int LeadsReceived { get; set; }
    public int LeadsAssigned { get; set; }
    public int LeadsCompleted { get; set; }
    public double ConversionRate { get; set; }
    public double AvgDaysToComplete { get; set; }
}

// TeamMemberDto para el dashboard de supervisor (similar al admin)
public class SupervisorTeamMemberDto
{
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public int Quotations { get; set; }
    public int Reservations { get; set; }
    public double Efficiency { get; set; }
}

// DTO principal del dashboard de supervisor
public class SupervisorDashboardDto
{
    public SupervisorLeadsKpiDto LeadsKpi { get; set; } = new();
    public TeamMetricsDto TeamMetrics { get; set; } = new();
    public List<AdvisorPerformanceDto> Advisors { get; set; } = new();
    public List<SupervisorTeamMemberDto> TeamData { get; set; } = new();
    public List<RecentLeadDto> RecentLeads { get; set; } = new();
    public List<ConversionFunnelDto> ConversionFunnel { get; set; } = new();
    public List<LeadSourceDistributionDto> LeadSources { get; set; } = new();
    public List<WeeklyActivityDto> WeeklyActivity { get; set; } = new();
    public List<TaskAnalysisDto> TaskAnalysis { get; set; } = new();
    public List<ProjectLeadsAnalysisDto> ProjectLeads { get; set; } = new();
}
