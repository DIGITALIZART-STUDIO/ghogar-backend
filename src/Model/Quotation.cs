using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace GestionHogar.Model;

// Clase base para entidades sin IsActive
public abstract class BaseModelWithoutActive : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Implementamos IsActive para cumplir con IEntity pero lo ocultamos
    bool IEntity.IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}

public class Quotation : BaseModelWithoutActive
{
    // Código único de cotización
    [Required]
    public required string Code { get; set; }

    // Relación con el lead
    [Required]
    public Guid LeadId { get; set; }

    [ForeignKey("LeadId")]
    public Lead? Lead { get; set; }

    // **RELACIÓN PRINCIPAL: Conectar directamente con el lote**
    [Required]
    public Guid LotId { get; set; }

    [ForeignKey("LotId")]
    public Lot? Lot { get; set; }

    // Usuario asesor que genera la cotización
    [Required]
    public Guid AdvisorId { get; set; }

    [ForeignKey("AdvisorId")]
    public User? Advisor { get; set; }

    // Estado de la cotización
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public QuotationStatus Status { get; set; } = QuotationStatus.ISSUED;

    // **SOLO DATOS QUE NO CAMBIAN O SON ESPECÍFICOS DE LA COTIZACIÓN**
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPrice { get; set; } // Precio AL MOMENTO de la cotización (histórico)

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Discount { get; set; } = 0; // Descuento aplicado en esta cotización

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal FinalPrice { get; set; } // Precio final de esta cotización

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal DownPayment { get; set; } // Porcentaje negociado en esta cotización

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountFinanced { get; set; } // Monto específico de esta cotización

    [Required]
    public int MonthsFinanced { get; set; } // Meses negociados en esta cotización

    // **DATOS HISTÓRICOS AL MOMENTO DE LA COTIZACIÓN**
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal AreaAtQuotation { get; set; } // Área AL MOMENTO de cotizar

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal PricePerM2AtQuotation { get; set; } // Precio/m² AL MOMENTO de cotizar

    // Información financiera
    [Required]
    [StringLength(3)]
    public required string Currency { get; set; } // Moneda de esta cotización

    [Required]
    [Column(TypeName = "decimal(18,6)")]
    public decimal ExchangeRate { get; set; } = 1.0m; // T.C. de esta cotización

    [Required]
    public string QuotationDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");

    [Required]
    public DateTime ValidUntil { get; set; }

    // **PROPIEDADES CALCULADAS (NO PERSISTIDAS)**
    [NotMapped]
    public string ProjectName => Lot?.Block?.Project?.Name ?? "Proyecto no encontrado";

    [NotMapped]
    public string BlockName => Lot?.Block?.Name ?? "Bloque no encontrado";

    [NotMapped]
    public string LotNumber => Lot?.LotNumber ?? "Lote no encontrado";

    [NotMapped]
    public decimal CurrentLotArea => Lot?.Area ?? AreaAtQuotation;

    [NotMapped]
    public decimal CurrentLotPrice => Lot?.Price ?? TotalPrice;

    [NotMapped]
    public bool LotStillExists => Lot != null;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QuotationStatus
{
    ISSUED, // Emitida
    ACCEPTED, // Aceptada
    CANCELED, // Cancelada
}
