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

    // Nombre del proyecto
    [Required]
    public required string ProjectName { get; set; }

    // Usuario asesor que genera la cotización
    [Required]
    public Guid AdvisorId { get; set; }

    [ForeignKey("AdvisorId")]
    public User? Advisor { get; set; }

    // Estado de la cotización
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public QuotationStatus Status { get; set; } = QuotationStatus.ISSUED;

    // Precios y descuentos
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPrice { get; set; } // Precio de lista

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Discount { get; set; } = 0; // Descuento aplicado (DSC P.Lista)

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal FinalPrice { get; set; } // Precio final luego del descuento

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal DownPayment { get; set; } // Porcentaje de inicial

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountFinanced { get; set; } // Monto a financiar

    [Required]
    public int MonthsFinanced { get; set; } // Nro de meses de financiamiento

    // Datos del lote
    [Required]
    public required string Block { get; set; } // Manzana

    [Required]
    public required string LotNumber { get; set; } // Lote

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Area { get; set; } // Área del lote

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal PricePerM2 { get; set; } // Precio por m²

    // Tipo de cambio
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal ExchangeRate { get; set; } // T.C. referencial

    [Required]
    public string QuotationDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd"); // Fecha de cotización

    // Fecha límite de validez
    [Required]
    public DateTime ValidUntil { get; set; } // Fecha límite de validez de la cotización
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QuotationStatus
{
    ISSUED, // Emitida
    ACCEPTED, // Aceptada
    CANCELED, // Cancelada
}
