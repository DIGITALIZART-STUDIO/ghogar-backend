// DTOs para el dashboard del Finance Manager
public class FinancialSummaryDto
{
    public decimal TotalInvoiced { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal PendingCollection { get; set; }
    public decimal Overdue { get; set; }
    public decimal CurrentLiquidity { get; set; }
    public decimal MonthlyProjection { get; set; }
    public double GrossMargin { get; set; }
    public double PortfolioTurnover { get; set; }
}

public class AccountsReceivableDto
{
    public string Project { get; set; } = "";
    public decimal Invoiced { get; set; }
    public decimal Collected { get; set; }
    public decimal Pending { get; set; }
    public decimal Overdue { get; set; }
    public decimal NextDue { get; set; }
    public string NextPaymentDate { get; set; } = "";
}

public class MonthlyIncomeDto
{
    public string Month { get; set; } = "";
    public decimal Collected { get; set; }
    public decimal Accumulated { get; set; }
    public int Projects { get; set; }
}

public class PaymentScheduleDto
{
    public string Client { get; set; } = "";
    public string Project { get; set; } = "";
    public decimal Amount { get; set; }
    public string DueDate { get; set; } = "";
    public int DaysOverdue { get; set; }
    public string Status { get; set; } = "";
    public string Installment { get; set; } = "";
    public string Lot { get; set; } = "";
}

public class DelinquencyAnalysisDto
{
    public string Range { get; set; } = "";
    public decimal Amount { get; set; }
    public int Quantity { get; set; }
    public double Percentage { get; set; }
}

public class IncomeProjectionDto
{
    public string Month { get; set; } = "";
    public decimal Conservative { get; set; }
    public decimal Realistic { get; set; }
    public decimal Optimistic { get; set; }
}

public class KpiIndicatorsDto
{
    public double PortfolioDays { get; set; }
    public double LiquidityIndex { get; set; }
    public double AssetTurnover { get; set; }
    public double OperatingMargin { get; set; }
    public double MonthlyGrowth { get; set; }
    public double CollectionEfficiency { get; set; }
}

public class FinanceManagerDashboardDto
{
    public FinancialSummaryDto FinancialSummary { get; set; } = new();
    public List<AccountsReceivableDto> AccountsReceivable { get; set; } = new();
    public List<MonthlyIncomeDto> MonthlyIncome { get; set; } = new();
    public List<PaymentScheduleDto> PaymentSchedule { get; set; } = new();
    public List<DelinquencyAnalysisDto> DelinquencyAnalysis { get; set; } = new();
    public List<IncomeProjectionDto> IncomeProjection { get; set; } = new();
    public KpiIndicatorsDto KpiIndicators { get; set; } = new();
}
