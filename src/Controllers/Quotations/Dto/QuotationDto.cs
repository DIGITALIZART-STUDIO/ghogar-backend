using System;
using System.Text.Json.Serialization;
using GestionHogar.Model;

namespace GestionHogar.Dtos;

public class QuotationDTO
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;

    // Lead information
    public Guid LeadId { get; set; }
    public Guid ClientId { get; set; }
    public string LeadClientName { get; set; } = string.Empty;

    // Lot information (actual)
    public Guid LotId { get; set; }
    public Guid ProjectId { get; set; } // **NUEVO: ID del proyecto**
    public Guid BlockId { get; set; } // **NUEVO: ID del bloque**
    public string ProjectName { get; set; } = string.Empty; // Nombre actual del proyecto
    public string BlockName { get; set; } = string.Empty; // Nombre actual del bloque
    public string LotNumber { get; set; } = string.Empty; // Número actual del lote

    // Advisor information
    public Guid AdvisorId { get; set; }
    public string AdvisorName { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public QuotationStatus Status { get; set; }
    public string StatusText { get; set; } = string.Empty;

    // Financial information (histórica de la cotización)
    public decimal TotalPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal FinalPrice { get; set; }
    public decimal DownPayment { get; set; }
    public decimal AmountFinanced { get; set; }
    public int MonthsFinanced { get; set; }

    // Lot information (histórica al momento de cotización)
    public decimal AreaAtQuotation { get; set; }
    public decimal PricePerM2AtQuotation { get; set; }

    // Lot information (actual)
    public decimal? CurrentLotArea { get; set; }
    public decimal? CurrentLotPrice { get; set; }
    public bool LotStillExists { get; set; }

    // Financial information
    public string Currency { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; }

    public string QuotationDate { get; set; } = string.Empty;
    public DateTime ValidUntil { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }

    public static QuotationDTO FromEntity(Quotation quotation)
    {
        // Obtener ProjectId y BlockId desde la relación Lot
        var projectId = quotation.Lot?.Block?.ProjectId ?? Guid.Empty;
        var blockId = quotation.Lot?.BlockId ?? Guid.Empty;

        return new QuotationDTO
        {
            Id = quotation.Id,
            Code = quotation.Code,
            LeadId = quotation.LeadId,
            ClientId = quotation.Lead?.ClientId ?? Guid.Empty,
            LeadClientName = quotation.Lead?.Client?.Name ?? "Cliente no especificado",
            LotId = quotation.LotId,
            ProjectId = projectId, // **NUEVO: ID del proyecto**
            BlockId = blockId, // **NUEVO: ID del bloque**
            ProjectName = quotation.Lot?.Block?.Project?.Name ?? "Proyecto no especificado",
            BlockName = quotation.Lot?.Block?.Name ?? "Bloque no especificado",
            LotNumber = quotation.Lot?.LotNumber ?? "Lote no especificado",
            AdvisorId = quotation.AdvisorId,
            AdvisorName = quotation.Advisor?.Name ?? "Asesor no especificado",
            Status = quotation.Status,
            StatusText = GetStatusText(quotation.Status),
            TotalPrice = quotation.TotalPrice,
            Discount = quotation.Discount,
            FinalPrice = quotation.FinalPrice,
            DownPayment = quotation.DownPayment,
            AmountFinanced = quotation.AmountFinanced,
            MonthsFinanced = quotation.MonthsFinanced,
            AreaAtQuotation = quotation.AreaAtQuotation,
            PricePerM2AtQuotation = quotation.PricePerM2AtQuotation,
            CurrentLotArea = quotation.CurrentLotArea,
            CurrentLotPrice = quotation.CurrentLotPrice,
            LotStillExists = quotation.LotStillExists,
            Currency = quotation.Currency,
            ExchangeRate = quotation.ExchangeRate,
            QuotationDate = quotation.QuotationDate,
            ValidUntil = quotation.ValidUntil,
            CreatedAt = quotation.CreatedAt,
            ModifiedAt = quotation.ModifiedAt,
        };
    }

    private static string GetStatusText(QuotationStatus status)
    {
        return status switch
        {
            QuotationStatus.ISSUED => "Emitida",
            QuotationStatus.ACCEPTED => "Aceptada",
            QuotationStatus.CANCELED => "Cancelada",
            _ => "Desconocido",
        };
    }
}
