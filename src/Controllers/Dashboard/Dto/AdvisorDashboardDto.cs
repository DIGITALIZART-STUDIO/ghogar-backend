// DTOs para el dashboard de asesores
public class MyLeadsDto
{
    public int Total { get; set; }
    public int Registered { get; set; }
    public int Attended { get; set; }
    public int InFollowUp { get; set; }
    public int Completed { get; set; }
    public int Canceled { get; set; }
    public int Expired { get; set; }
}

public class PerformanceDto
{
    public double ConversionRate { get; set; }
    public double AvgResponseTime { get; set; }
    public int QuotationsIssued { get; set; }
    public int ReservationsGenerated { get; set; }
    public int TasksCompleted { get; set; }
    public int TasksPending { get; set; }
}

public class AssignedLeadDto
{
    public Guid Id { get; set; }
    public string ClientName { get; set; } = "";
    public string ClientPhone { get; set; } = "";
    public string? ClientEmail { get; set; }
    public string CaptureSource { get; set; } = "";
    public string Status { get; set; } = "";
    public int DaysUntilExpiration { get; set; }
    public string ProjectName { get; set; } = "";
    public DateTime EntryDate { get; set; }
    public DateTime? LastContact { get; set; }
    public string NextTask { get; set; } = "";
    public string Priority { get; set; } = "";
}

public class MyTaskDto
{
    public Guid Id { get; set; }
    public Guid LeadId { get; set; }
    public string ClientName { get; set; } = "";
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime ScheduledDate { get; set; }
    public bool IsCompleted { get; set; }
    public string Priority { get; set; } = "";
}

public class MyQuotationDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string ClientName { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public string LotNumber { get; set; } = "";
    public decimal TotalPrice { get; set; }
    public decimal FinalPrice { get; set; }
    public string Status { get; set; } = "";
    public string QuotationDate { get; set; } = "";
    public DateTime ValidUntil { get; set; }
    public string Currency { get; set; } = "";
}

public class MyReservationDto
{
    public Guid Id { get; set; }
    public string ClientName { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public string LotNumber { get; set; } = "";
    public decimal AmountPaid { get; set; }
    public string Currency { get; set; } = "";
    public string Status { get; set; } = "";
    public string PaymentMethod { get; set; } = "";
    public DateOnly ReservationDate { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool Notified { get; set; }
}

public class AdvisorMonthlyPerformanceDto
{
    public string Month { get; set; } = "";
    public int LeadsAssigned { get; set; }
    public int LeadsCompleted { get; set; }
    public int Quotations { get; set; }
    public int Reservations { get; set; }
}

public class MyLeadSourceDto
{
    public string Source { get; set; } = "";
    public int Count { get; set; }
    public int Converted { get; set; }
    public string Color { get; set; } = "";
}

public class TasksByTypeDto
{
    public string Type { get; set; } = "";
    public int Scheduled { get; set; }
    public int Completed { get; set; }
    public int Pending { get; set; }
}

public class MyProjectDto
{
    public string Project { get; set; } = "";
    public int LeadsAssigned { get; set; }
    public int LeadsCompleted { get; set; }
    public int QuotationsIssued { get; set; }
    public int ReservationsMade { get; set; }
    public double ConversionRate { get; set; }
}

public class AdvisorDashboardDto
{
    public MyLeadsDto MyLeads { get; set; } = new();
    public PerformanceDto Performance { get; set; } = new();
    public List<AssignedLeadDto> AssignedLeads { get; set; } = new();
    public List<MyTaskDto> MyTasks { get; set; } = new();
    public List<MyQuotationDto> MyQuotations { get; set; } = new();
    public List<MyReservationDto> MyReservations { get; set; } = new();
    public List<AdvisorMonthlyPerformanceDto> MonthlyPerformance { get; set; } = new();
    public List<MyLeadSourceDto> MyLeadSources { get; set; } = new();
    public List<TasksByTypeDto> TasksByType { get; set; } = new();
    public List<MyProjectDto> MyProjects { get; set; } = new();
}
