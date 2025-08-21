using System;
using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;

namespace GestionHogar.Dtos;

public class QuotationCreateDTO
{
    [Required]
    public Guid LeadId { get; set; }

    [Required]
    public Guid LotId { get; set; } // **NUEVO: Ahora referenciamos directamente el lote**

    // Datos financieros opcionales (si no se especifican, se usan los del proyecto)
    public decimal? Discount { get; set; } = 0;
    public decimal? DownPayment { get; set; } // Si no se especifica, usa DefaultDownPayment del proyecto
    public int? MonthsFinanced { get; set; } // Si no se especifica, usa DefaultFinancingMonths del proyecto
    public decimal? ExchangeRate { get; set; } = 1.0m;

    // Fecha de cotización (opcional, por defecto hoy)
    public string? QuotationDate { get; set; }

    // Días de validez (opcional, por defecto 30 días)
    public int ValidityDays { get; set; } = 30;

    public Quotation ToEntity(string code, Lot lot)
    {
        // Usar datos del lote y proyecto para llenar automáticamente
        var project = lot.Block.Project;

        // Fechas
        DateTime quotationDateTime = DateTime.UtcNow;
        if (
            !string.IsNullOrEmpty(QuotationDate) && DateTime.TryParse(QuotationDate, out var parsed)
        )
        {
            quotationDateTime = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        // Cálculos financieros
        var discount = Discount ?? 0;
        var finalPrice = lot.Price - discount;
        var downPaymentPercentage = DownPayment ?? project.DefaultDownPayment ?? 10;
        var monthsFinanced = MonthsFinanced ?? project.DefaultFinancingMonths ?? 36;
        var downPaymentAmount = finalPrice * (downPaymentPercentage / 100);
        var amountFinanced = finalPrice - downPaymentAmount;

        return new Quotation
        {
            Code = code,
            LeadId = LeadId,
            LotId = LotId,
            AdvisorId = Guid.Empty, // Se establecerá después con el usuario actual
            Status = QuotationStatus.ISSUED,

            // Precios (históricos al momento de la cotización)
            TotalPrice = lot.Price,
            Discount = discount,
            FinalPrice = finalPrice,
            DownPayment = downPaymentPercentage,
            AmountFinanced = amountFinanced,
            MonthsFinanced = monthsFinanced,

            // Datos históricos del lote al momento de cotización
            AreaAtQuotation = lot.Area,
            PricePerM2AtQuotation = lot.Area > 0 ? lot.Price / lot.Area : 0,

            // Información financiera
            Currency = project.Currency,
            ExchangeRate = ExchangeRate ?? 1.0m,

            // Fechas
            QuotationDate = quotationDateTime.ToString("yyyy-MM-dd"),
            ValidUntil = quotationDateTime.AddDays(ValidityDays),
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
        };
    }
}
