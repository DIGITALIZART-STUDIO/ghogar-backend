using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Model;

[Index(nameof(Code), IsUnique = true, Name = "IX_Contract_Code")]
[Index(nameof(ReservationId), IsUnique = true, Name = "IX_Contract_ReservationId")]
public class Contract : BaseModel
{
    [Required]
    [StringLength(32)]
    public required string Code { get; set; }

    [Required]
    public Guid ClientId { get; set; }

    [ForeignKey(nameof(ClientId))]
    public Client Client { get; set; } = null!;

    [Required]
    public Guid LotId { get; set; }

    [ForeignKey(nameof(LotId))]
    public Lot Lot { get; set; } = null!;

    public Guid? ReservationId { get; set; }

    [ForeignKey(nameof(ReservationId))]
    public Reservation? Reservation { get; set; }

    public Guid? QuotationId { get; set; }

    [ForeignKey(nameof(QuotationId))]
    public Quotation? Quotation { get; set; }

    public Guid? AdvisorId { get; set; }

    [ForeignKey(nameof(AdvisorId))]
    public User? Advisor { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ContractOrigin Origin { get; set; } = ContractOrigin.Pipeline;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ContractStatus Status { get; set; } = ContractStatus.Draft;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DownPaymentAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountFinanced { get; set; }

    public int MonthsFinanced { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Currency Currency { get; set; } = Currency.SOLES;

    [Column(TypeName = "decimal(18,6)")]
    public decimal ExchangeRate { get; set; } = 1.0m;

    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    public DateTime? SignedAt { get; set; }

    [Column(TypeName = "jsonb")]
    public string? CoOwners { get; set; }

    public ICollection<Payment> Payments { get; set; } = [];

    public ICollection<PaymentTransaction> PaymentTransactions { get; set; } = [];
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContractOrigin
{
    Pipeline,
    Direct,
    Legacy,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContractStatus
{
    Draft,
    PendingValidation,
    Active,
    Completed,
    Rescinded,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentType
{
    DownPayment,
    Installment,
    Extra,
}
