// DTOs para el dashboard de Manager (vista estratégica del negocio)
namespace GestionHogar.Controllers;

// KPIs estratégicos principales
public class ManagerKpisDto
{
    public int TotalProjects { get; set; }
    public int ActiveProjects { get; set; }
    public int TotalLeads { get; set; }
    public int TotalQuotations { get; set; }
    public int TotalReservations { get; set; }
    public decimal TotalReservationAmount { get; set; }
    public decimal ConversionRate { get; set; }
    public int ActiveAdvisors { get; set; }
}

// Rendimiento por proyecto
public class ProjectPerformanceDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = "";
    public int TotalLeads { get; set; }
    public int CompletedLeads { get; set; }
    public int Quotations { get; set; }
    public int Reservations { get; set; }
    public decimal ReservationAmount { get; set; }
    public decimal ConversionRate { get; set; }
    public int AvailableUnits { get; set; }
    public int ReservedUnits { get; set; }
    public decimal OccupancyRate { get; set; }
}

// Rendimiento del equipo de ventas
public class SalesTeamPerformanceDto
{
    public Guid AdvisorId { get; set; }
    public string AdvisorName { get; set; } = "";
    public int LeadsAssigned { get; set; }
    public int LeadsCompleted { get; set; }
    public int QuotationsIssued { get; set; }
    public int ReservationsGenerated { get; set; }
    public decimal ReservationAmount { get; set; }
    public decimal ConversionRate { get; set; }
    public decimal AvgResponseTime { get; set; }
}

// Análisis de fuentes de captación
public class LeadSourceAnalysisDto
{
    public string Source { get; set; } = "";
    public int TotalLeads { get; set; }
    public int ConvertedLeads { get; set; }
    public decimal ConversionRate { get; set; }
    public decimal CostPerLead { get; set; }
    public decimal ROI { get; set; }
}

// Análisis mensual de tendencias
public class MonthlyTrendDto
{
    public string Month { get; set; } = "";
    public int Year { get; set; }
    public int LeadsReceived { get; set; }
    public int QuotationsIssued { get; set; }
    public int ReservationsMade { get; set; }
    public decimal ReservationAmount { get; set; }
    public decimal ConversionRate { get; set; }
}

// Estado del pipeline de ventas
public class SalesPipelineDto
{
    public int NewLeads { get; set; }
    public int InContact { get; set; }
    public int QuotationStage { get; set; }
    public int NegotiationStage { get; set; }
    public int ReservationStage { get; set; }
    public int ClosedWon { get; set; }
    public int ClosedLost { get; set; }
}

// Análisis de cotizaciones
public class QuotationAnalysisDto
{
    public int TotalIssued { get; set; }
    public int Accepted { get; set; }
    public int Pending { get; set; }
    public int Rejected { get; set; }
    public decimal AcceptanceRate { get; set; }
    public decimal AvgQuotationAmount { get; set; }
    public decimal TotalQuotationValue { get; set; }
}

// Análisis de reservaciones
public class ReservationAnalysisDto
{
    public int TotalReservations { get; set; }
    public int ActiveReservations { get; set; }
    public int CanceledReservations { get; set; }
    public int AnulatedReservations { get; set; }
    public decimal TotalReservationAmount { get; set; }
    public decimal AvgReservationAmount { get; set; }
    public decimal CancellationRate { get; set; }
}

// Métricas de tiempo
public class TimeMetricsDto
{
    public decimal AvgLeadToQuotation { get; set; } // Días
    public decimal AvgQuotationToReservation { get; set; } // Días
    public decimal AvgLeadToReservation { get; set; } // Días
    public decimal AvgResponseTime { get; set; } // Horas
}

// Alertas y notificaciones para el manager
public class ManagerAlertDto
{
    public string Type { get; set; } = ""; // warning, danger, info
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public int Count { get; set; }
    public string Priority { get; set; } = ""; // high, medium, low
}

// Top performers
public class TopPerformerDto
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = ""; // advisor, project, source
    public decimal Value { get; set; }
    public string Metric { get; set; } = ""; // conversions, revenue, leads
}

// DTO principal del dashboard de Manager
public class ManagerDashboardDto
{
    public ManagerKpisDto Kpis { get; set; } = new();
    public List<ProjectPerformanceDto> ProjectPerformance { get; set; } = new();
    public List<SalesTeamPerformanceDto> SalesTeamPerformance { get; set; } = new();
    public List<LeadSourceAnalysisDto> LeadSourceAnalysis { get; set; } = new();
    public List<MonthlyTrendDto> MonthlyTrends { get; set; } = new();
    public SalesPipelineDto SalesPipeline { get; set; } = new();
    public QuotationAnalysisDto QuotationAnalysis { get; set; } = new();
    public ReservationAnalysisDto ReservationAnalysis { get; set; } = new();
    public TimeMetricsDto TimeMetrics { get; set; } = new();
    public List<ManagerAlertDto> Alerts { get; set; } = new();
    public List<TopPerformerDto> TopPerformers { get; set; } = new();
}
