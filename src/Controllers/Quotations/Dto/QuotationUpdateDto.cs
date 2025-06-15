using System;
using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;

namespace GestionHogar.Dtos;

public class QuotationUpdateDTO
{
    // Eliminamos Code ya que no se debe actualizar
    public string? ProjectName { get; set; }
    public Guid? AdvisorId { get; set; }
    public string? Status { get; set; }

    // Financial information
    public decimal? TotalPrice { get; set; }
    public decimal? Discount { get; set; }
    public decimal? FinalPrice { get; set; }
    public decimal? DownPayment { get; set; }
    public decimal? AmountFinanced { get; set; }
    public int? MonthsFinanced { get; set; }

    // Lot information
    public string? Block { get; set; }
    public string? LotNumber { get; set; }
    public decimal? Area { get; set; }
    public decimal? PricePerM2 { get; set; }
    public decimal? ExchangeRate { get; set; }

    public DateTime? ValidUntil { get; set; }

    // Nuevo campo para fecha de cotización
    public string? QuotationDate { get; set; }

    public void ApplyTo(Quotation entity)
    {
        if (ProjectName != null)
            entity.ProjectName = ProjectName;
        if (AdvisorId.HasValue)
            entity.AdvisorId = AdvisorId.Value;
        if (Status != null && Enum.TryParse<QuotationStatus>(Status, out var status))
            entity.Status = status;

        if (TotalPrice.HasValue)
            entity.TotalPrice = TotalPrice.Value;
        if (Discount.HasValue)
            entity.Discount = Discount.Value;
        if (FinalPrice.HasValue)
            entity.FinalPrice = FinalPrice.Value;
        if (DownPayment.HasValue)
            entity.DownPayment = DownPayment.Value;
        if (AmountFinanced.HasValue)
            entity.AmountFinanced = AmountFinanced.Value;
        if (MonthsFinanced.HasValue)
            entity.MonthsFinanced = MonthsFinanced.Value;

        if (Block != null)
            entity.Block = Block;
        if (LotNumber != null)
            entity.LotNumber = LotNumber;
        if (Area.HasValue)
            entity.Area = Area.Value;
        if (PricePerM2.HasValue)
            entity.PricePerM2 = PricePerM2.Value;
        if (ExchangeRate.HasValue)
            entity.ExchangeRate = ExchangeRate.Value;

        if (ValidUntil.HasValue)
        {
            // Asegurar que la fecha sea UTC
            entity.ValidUntil = DateTime.SpecifyKind(ValidUntil.Value, DateTimeKind.Utc);
        }

        if (QuotationDate != null)
        {
            entity.QuotationDate = QuotationDate;

            // Si se actualiza la fecha de cotización, también actualizamos ValidUntil
            DateTime quotationDateTime;
            if (DateTime.TryParse(QuotationDate, out quotationDateTime))
            {
                // Convertir a UTC y recalcular ValidUntil
                quotationDateTime = DateTime.SpecifyKind(quotationDateTime, DateTimeKind.Utc);
                entity.ValidUntil = quotationDateTime.AddDays(5);
            }
        }

        // Actualizar la fecha de modificación con UTC
        entity.ModifiedAt = DateTime.UtcNow;
    }
}
