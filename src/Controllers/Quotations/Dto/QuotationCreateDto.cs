using System;
using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;

namespace GestionHogar.Dtos;

public class QuotationCreateDTO
{
    // Eliminamos el Code ya que se generará automáticamente

    [Required]
    public Guid LeadId { get; set; }

    [Required]
    public required string ProjectName { get; set; }

    [Required]
    public Guid AdvisorId { get; set; }

    // Financial information
    [Required]
    public decimal TotalPrice { get; set; }

    [Required]
    public decimal Discount { get; set; }

    [Required]
    public decimal FinalPrice { get; set; }

    [Required]
    public decimal DownPayment { get; set; }

    [Required]
    public decimal AmountFinanced { get; set; }

    [Required]
    public int MonthsFinanced { get; set; }

    // Lot information
    [Required]
    public required string Block { get; set; }

    [Required]
    public required string LotNumber { get; set; }

    [Required]
    public decimal Area { get; set; }

    [Required]
    public decimal PricePerM2 { get; set; }

    [Required]
    public decimal ExchangeRate { get; set; }

    // Quitamos ValidUntil como campo requerido ya que se calculará automáticamente

    // Fecha de cotización
    [Required]
    public string QuotationDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");

    public Quotation ToEntity(string code)
    {
        // Convertir QuotationDate (string) a DateTime
        DateTime quotationDateTime;
        if (!DateTime.TryParse(QuotationDate, out quotationDateTime))
        {
            // Si hay un error en el formato, usamos la fecha actual
            quotationDateTime = DateTime.UtcNow;
        }
        else
        {
            // Convertir explícitamente a UTC
            quotationDateTime = DateTime.SpecifyKind(quotationDateTime, DateTimeKind.Utc);
        }

        // Calcular ValidUntil como QuotationDate + 5 días (asegurando que sea UTC)
        DateTime validUntil = DateTime.SpecifyKind(quotationDateTime.AddDays(5), DateTimeKind.Utc);

        return new Quotation
        {
            Code = code, // Ahora recibimos el código como parámetro
            LeadId = LeadId,
            ProjectName = ProjectName,
            AdvisorId = AdvisorId,
            Status = QuotationStatus.ISSUED, // Por defecto, al crear una cotización está "Emitida"
            TotalPrice = TotalPrice,
            Discount = Discount,
            FinalPrice = FinalPrice,
            DownPayment = DownPayment,
            AmountFinanced = AmountFinanced,
            MonthsFinanced = MonthsFinanced,
            Block = Block,
            LotNumber = LotNumber,
            Area = Area,
            PricePerM2 = PricePerM2,
            ExchangeRate = ExchangeRate,
            ValidUntil = validUntil, // Asignamos la fecha calculada UTC
            QuotationDate = QuotationDate,
            // Asegurar que las fechas de auditoría sean UTC
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
        };
    }
}
