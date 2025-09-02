using GestionHogar.Model;

namespace GestionHogar.Controllers.Dtos;

public class ReservationWithPendingPaymentsDto
{
    public Guid Id { get; set; }
    public DateTime ReservationDate { get; set; }
    public decimal AmountPaid { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public ReservationStatus Status { get; set; }
    public Currency Currency { get; set; }
    public decimal ExchangeRate { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }

    // Información del cliente
    public ClientDto Client { get; set; } = new();

    // Información del lote
    public LotDto Lot { get; set; } = new();

    // Información del proyecto
    public ProjectDto Project { get; set; } = new();

    // Información de la cotización
    public QuotationDto Quotation { get; set; } = new();

    // Cuotas pendientes
    public List<PendingPaymentDto> PendingPayments { get; set; } = new();

    // Resumen de pagos
    public decimal TotalAmountDue { get; set; }
    public decimal TotalAmountPaid { get; set; }
    public decimal TotalRemainingAmount { get; set; }
    public int TotalPendingQuotas { get; set; }
    public DateTime? NextPaymentDueDate { get; set; }
}

public class ClientDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Dni { get; set; } = string.Empty;
    public string? Ruc { get; set; }
    public string? Email { get; set; }
    public string PhoneNumber { get; set; } = string.Empty; // Campo real del modelo
}

public class LotDto
{
    public Guid Id { get; set; }
    public string LotNumber { get; set; } = string.Empty; // Campo real del modelo
    public decimal Area { get; set; }
    public decimal Price { get; set; }
    // No tiene Description en el modelo real
}

public class ProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty; // Campo real del modelo
    // No tiene Description ni Address en el modelo real
}

public class QuotationDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal FinalPrice { get; set; } // Campo real del modelo
    public int MonthsFinanced { get; set; } // Campo real del modelo
    public decimal QuotaAmount { get; set; } // Calculado: FinalPrice / MonthsFinanced
}

public class PendingPaymentDto
{
    public Guid Id { get; set; }
    public DateTime DueDate { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal RemainingAmount { get; set; }
    public bool IsOverdue { get; set; }
}
