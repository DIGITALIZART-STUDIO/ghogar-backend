using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using GestionHogar.Model;

namespace GestionHogar.Dtos;

public class QuotationUpdateDTO
{
    public Guid? AdvisorId { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public QuotationStatus? Status { get; set; }

    // Financial information (solo datos específicos de la cotización)
    public decimal? Discount { get; set; }
    public decimal? DownPayment { get; set; }
    public int? MonthsFinanced { get; set; }
    public decimal? ExchangeRate { get; set; }
    public DateTime? ValidUntil { get; set; }
    public string? QuotationDate { get; set; }

    public void ApplyTo(Quotation entity)
    {
        if (AdvisorId.HasValue)
            entity.AdvisorId = AdvisorId.Value;

        if (Status.HasValue)
            entity.Status = Status.Value;

        if (Discount.HasValue)
        {
            entity.Discount = Discount.Value;
            // Recalcular precio final
            entity.FinalPrice = entity.TotalPrice - entity.Discount;
            // Recalcular monto financiado
            var downPaymentAmount = entity.FinalPrice * (entity.DownPayment / 100);
            entity.AmountFinanced = entity.FinalPrice - downPaymentAmount;
        }

        if (DownPayment.HasValue)
        {
            entity.DownPayment = DownPayment.Value;
            // Recalcular monto financiado
            var downPaymentAmount = entity.FinalPrice * (entity.DownPayment / 100);
            entity.AmountFinanced = entity.FinalPrice - downPaymentAmount;
        }

        if (MonthsFinanced.HasValue)
            entity.MonthsFinanced = MonthsFinanced.Value;

        if (ExchangeRate.HasValue)
            entity.ExchangeRate = ExchangeRate.Value;

        if (ValidUntil.HasValue)
            entity.ValidUntil = DateTime.SpecifyKind(ValidUntil.Value, DateTimeKind.Utc);

        if (!string.IsNullOrEmpty(QuotationDate))
        {
            entity.QuotationDate = QuotationDate;
            if (DateTime.TryParse(QuotationDate, out var quotationDateTime))
            {
                quotationDateTime = DateTime.SpecifyKind(quotationDateTime, DateTimeKind.Utc);
                entity.ValidUntil = quotationDateTime.AddDays(30); // Recalcular validez
            }
        }

        entity.ModifiedAt = DateTime.UtcNow;
    }
}
