using System.Text.Json.Serialization;
using GestionHogar.Model;

namespace GestionHogar.Controllers.Reports.Dto;

/// <summary>
/// Fila del reporte administrativo de créditos: un contrato activo.
/// </summary>
public class CreditsReportItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;

    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? ClientDni { get; set; }

    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string LotNumber { get; set; } = string.Empty;
    public string BlockName { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Currency Currency { get; set; }

    public decimal TotalPrice { get; set; }
    public decimal DownPaymentAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal RemainingAmount { get; set; }
    public int MonthsFinanced { get; set; }

    public int PendingInstallmentCount { get; set; }
    public DateTime? NextPaymentDueDate { get; set; }
    public decimal NextPaymentAmount { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ContractOrigin Origin { get; set; }
}

/// <summary>
/// Fila del reporte administrativo de ingresos: un voucher / transacción de pago.
/// </summary>
public class IncomeReportItemDto
{
    public Guid Id { get; set; }
    public DateTimeOffset PaymentDate { get; set; }
    public decimal AmountPaid { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Currency Currency { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PaymentMethod PaymentMethod { get; set; }

    public string? ReferenceNumber { get; set; }

    public Guid? ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? ClientDni { get; set; }

    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }

    public Guid? ContractId { get; set; }
    public string? ContractCode { get; set; }
    public Guid? ReservationId { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public IncomeReportOrigin Origin { get; set; }

    public int QuotasCovered { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IncomeReportOrigin
{
    Contract,
    Reservation,
}

public class IncomeReportCurrencyTotalDto
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Currency Currency { get; set; }

    public int PaymentCount { get; set; }
    public decimal TotalAmount { get; set; }
    public int ReservationPaymentCount { get; set; }
    public decimal ReservationPaymentAmount { get; set; }
}

public class IncomeReportSummaryDto
{
    public int PaymentCount { get; set; }
    public decimal TotalAmount { get; set; }
    public int ReservationPaymentCount { get; set; }
    public decimal ReservationPaymentAmount { get; set; }
    public List<IncomeReportCurrencyTotalDto> TotalsByCurrency { get; set; } = [];
}

public class IncomeReportResponse
{
    public List<IncomeReportItemDto> Data { get; set; } = [];
    public PaginationMetadata Meta { get; set; } = new();
    public IncomeReportSummaryDto Summary { get; set; } = new();
}
