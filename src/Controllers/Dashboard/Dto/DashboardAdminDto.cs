public class LotStatusDto
{
    public string Status { get; set; } = "";
    public int Count { get; set; }
    public double Percentage { get; set; }
}

public class TeamMemberDto
{
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public int Quotations { get; set; }
    public int Reservations { get; set; }
    public double Efficiency { get; set; }
}

public class LeadStatusDto
{
    public string Status { get; set; } = "";
    public int Count { get; set; }
}

public class LeadSourceDto
{
    public string Source { get; set; } = "";
    public int Count { get; set; }
    public double Percentage { get; set; }
}

public class ClientAnalysisDto
{
    public int TotalClients { get; set; }
    public int NaturalPersons { get; set; }
    public int LegalEntities { get; set; }
    public int WithEmail { get; set; }
    public int WithCompleteData { get; set; }
    public int SeparateProperty { get; set; }
    public int CoOwners { get; set; }
}

public class ProjectMetricDto
{
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public int Blocks { get; set; }
    public int TotalLots { get; set; }
    public int Available { get; set; }
    public int Quoted { get; set; }
    public int Reserved { get; set; }
    public int Sold { get; set; }
    public decimal Revenue { get; set; }
    public decimal AvgPrice { get; set; }
    public double Efficiency { get; set; }
}

public class PaymentMetricsDto
{
    public decimal TotalScheduled { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal Pending { get; set; }
    public decimal Overdue { get; set; }
    public decimal CashPayments { get; set; }
    public decimal BankTransfers { get; set; }
    public decimal Deposits { get; set; }
}

public class MonthlyPerformanceDto
{
    public string Month { get; set; } = "";
    public int Leads { get; set; }
    public int Quotations { get; set; }
    public int Reservations { get; set; }
    public int Sales { get; set; }
    public decimal Revenue { get; set; }
}

public class DashboardAdminDto
{
    public int TotalProjects { get; set; }
    public int TotalBlocks { get; set; }
    public int TotalLots { get; set; }
    public int TotalClients { get; set; }
    public int ActiveLeads { get; set; }
    public int ExpiredLeads { get; set; }
    public int ActiveQuotations { get; set; }
    public int PendingReservations { get; set; }
    public int CompletedSales { get; set; }

    // Métricas financieras
    public decimal MonthlyRevenue { get; set; }
    public decimal PendingPayments { get; set; }
    public decimal AverageTicket { get; set; }
    public double ConversionRate { get; set; }
    public double OperationalEfficiency { get; set; }

    public List<LotStatusDto> LotsByStatus { get; set; } = new();
    public List<TeamMemberDto> TeamData { get; set; } = new();
    public List<LeadStatusDto> LeadsByStatus { get; set; } = new();
    public List<LeadSourceDto> LeadSources { get; set; } = new();
    public ClientAnalysisDto ClientAnalysis { get; set; } = new();
    public List<ProjectMetricDto> ProjectMetrics { get; set; } = new();
    public PaymentMetricsDto PaymentMetrics { get; set; } = new();
    public List<MonthlyPerformanceDto> MonthlyPerformance { get; set; } = new();
}
