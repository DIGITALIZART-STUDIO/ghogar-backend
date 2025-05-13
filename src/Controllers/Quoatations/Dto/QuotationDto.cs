using System;
using GestionHogar.Model;

namespace GestionHogar.Dtos;

public class QuotationDTO
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;

    // Lead information
    public Guid LeadId { get; set; }
    public string? LeadClientName { get; set; } // Nombre del cliente asociado al lead

    public string ProjectName { get; set; } = null!;

    // Advisor information
    public Guid AdvisorId { get; set; }
    public string? AdvisorName { get; set; }

    public string Status { get; set; } = null!;

    // Financial information
    public decimal TotalPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal FinalPrice { get; set; }
    public decimal DownPayment { get; set; }
    public decimal AmountFinanced { get; set; }
    public int MonthsFinanced { get; set; }

    // Lot information
    public string Block { get; set; } = null!;
    public string LotNumber { get; set; } = null!;
    public decimal Area { get; set; }
    public decimal PricePerM2 { get; set; }
    public decimal ExchangeRate { get; set; }

    public string QuotationDate { get; set; } = null!;

    public DateTime ValidUntil { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }

    public static QuotationDTO FromEntity(Quotation quotation)
    {
        return new QuotationDTO
        {
            Id = quotation.Id,
            Code = quotation.Code,
            LeadId = quotation.LeadId,
            LeadClientName = quotation.Lead?.Client?.Name, // Accedemos al nombre del cliente a través del lead
            ProjectName = quotation.ProjectName,
            AdvisorId = quotation.AdvisorId,
            AdvisorName = quotation.Advisor?.Name,
            Status = quotation.Status.ToString(),
            TotalPrice = quotation.TotalPrice,
            Discount = quotation.Discount,
            FinalPrice = quotation.FinalPrice,
            DownPayment = quotation.DownPayment,
            AmountFinanced = quotation.AmountFinanced,
            MonthsFinanced = quotation.MonthsFinanced,
            Block = quotation.Block,
            LotNumber = quotation.LotNumber,
            Area = quotation.Area,
            PricePerM2 = quotation.PricePerM2,
            ExchangeRate = quotation.ExchangeRate,
            QuotationDate = quotation.QuotationDate,
            ValidUntil = quotation.ValidUntil,
            CreatedAt = quotation.CreatedAt,
            ModifiedAt = quotation.ModifiedAt,
        };
    }
}
