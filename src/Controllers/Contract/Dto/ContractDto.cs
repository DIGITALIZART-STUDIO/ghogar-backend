using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;

namespace GestionHogar.Controllers.Dtos;

public class ContractDto
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public Guid LotId { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public string BlockName { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid? ReservationId { get; set; }
    public Guid? QuotationId { get; set; }
    public string? QuotationCode { get; set; }
    public ContractOrigin Origin { get; set; }
    public ContractStatus Status { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal DownPaymentAmount { get; set; }
    public decimal AmountFinanced { get; set; }
    public int MonthsFinanced { get; set; }
    public Currency Currency { get; set; }
    public decimal ExchangeRate { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? SignedAt { get; set; }
    public string? CoOwners { get; set; }
    public int PaymentCount { get; set; }
    public DateTime? NextPaymentDueDate { get; set; }
    public decimal NextPaymentAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal RemainingAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}

public class ContractBackfillResultDto
{
    public int ContractsCreated { get; set; }
    public int PaymentsLinked { get; set; }
    public int DownPaymentsCreated { get; set; }
    public int TransactionsLinked { get; set; }
    public List<string> Warnings { get; set; } = [];
}

public class CreateContractDto
{
    [Required]
    public ContractOrigin Origin { get; set; }

    [Required]
    public Guid ClientId { get; set; }

    [Required]
    public Guid LotId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal TotalPrice { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal DownPaymentAmount { get; set; }

    [Required]
    [Range(1, 360)]
    public int MonthsFinanced { get; set; }

    public Currency Currency { get; set; } = Currency.SOLES;

    [Range(0.000001, double.MaxValue)]
    public decimal ExchangeRate { get; set; } = 1m;

    public DateTime? StartDate { get; set; }

    public string? CoOwners { get; set; }

    /// <summary>
    /// Solo legado: cuotas de financiamiento ya pagadas (0..MonthsFinanced).
    /// </summary>
    [Range(0, 360)]
    public int PaidInstallments { get; set; }
}

public class UpdateContractDto
{
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal TotalPrice { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal DownPaymentAmount { get; set; }

    [Required]
    [Range(1, 360)]
    public int MonthsFinanced { get; set; }

    [Range(0.000001, double.MaxValue)]
    public decimal ExchangeRate { get; set; } = 1m;

    public DateTime? StartDate { get; set; }

    public string? CoOwners { get; set; }
}

public class ContractSchedulePreviewDto
{
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal TotalPrice { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal DownPaymentAmount { get; set; }

    [Required]
    [Range(1, 360)]
    public int MonthsFinanced { get; set; }

    public DateTime? StartDate { get; set; }
}

public class UpdateContractScheduleItemDto
{
    [Required]
    [Range(1, 360)]
    public int Sequence { get; set; }

    [Required]
    public DateTime DueDate { get; set; }
}

public class UpdateContractScheduleDto
{
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal TotalPrice { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal DownPaymentAmount { get; set; }

    [Required]
    [Range(1, 360)]
    public int MonthsFinanced { get; set; }

    [Range(0.000001, double.MaxValue)]
    public decimal ExchangeRate { get; set; } = 1m;

    public DateTime? StartDate { get; set; }

    public string? CoOwners { get; set; }

    public bool ApplyRecalculatedSchedule { get; set; }

    [Required]
    public List<UpdateContractScheduleItemDto> Installments { get; set; } = [];
}

public class ClientCurrencyBalanceDto
{
    public Currency Currency { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal OverdueAmount { get; set; }
    public decimal TotalPaid { get; set; }
}

public class ClientFinancialSummaryDto
{
    public int ActiveContracts { get; set; }
    public DateTime? NextPaymentDueDate { get; set; }
    public List<ClientCurrencyBalanceDto> Balances { get; set; } = [];
}
