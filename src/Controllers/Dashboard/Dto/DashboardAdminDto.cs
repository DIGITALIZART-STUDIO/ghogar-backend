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
}
