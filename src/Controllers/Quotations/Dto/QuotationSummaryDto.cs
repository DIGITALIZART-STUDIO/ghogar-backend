using System;
using System.Text.Json.Serialization;
using GestionHogar.Model;

namespace GestionHogar.Dtos;

public class QuotationSummaryDTO
{
    public required Guid Id { get; set; }
    public required string Code { get; set; } = null!;
    public required string ClientId { get; set; } = null!;
    public required string ClientName { get; set; } = null!;
    public string? ClientIdentification { get; set; }
    public string? ClientIdentificationType { get; set; }
    public required string ProjectName { get; set; } = null!;
    public required decimal TotalPrice { get; set; }
    public required decimal FinalPrice { get; set; }
    public required decimal AmountFinanced { get; set; }
    public required string BlockName { get; set; } = null!; // Cambiado de Block a BlockName
    public required string LotNumber { get; set; } = null!;
    public required decimal AreaAtQuotation { get; set; } // Cambiado de Area a AreaAtQuotation
    public required string Currency { get; set; } // Cambiado de Area a AreaAtQuotation

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public QuotationStatus Status { get; set; }
    public string StatusText { get; set; } = string.Empty;

    public decimal ExchangeRate { get; set; }

    public string QuotationDate { get; set; } = null!;
    public DateTime ValidUntil { get; set; }
    public DateTime CreatedAt { get; set; }

    public static QuotationSummaryDTO FromEntity(Quotation quotation)
    {
        var client = quotation.Lead?.Client;
        string? identification = null;
        string? identificationType = null;

        if (client != null)
        {
            if (client.Type == ClientType.Natural)
            {
                identification = client.Dni;
                identificationType = "DNI";
            }
            else
            {
                identification = client.Ruc;
                identificationType = "RUC";
            }
        }

        return new QuotationSummaryDTO
        {
            Id = quotation.Id,
            Code = quotation.Code,
            ClientId = quotation.Lead?.Client?.Id.ToString() ?? "Cliente no especificado",
            ClientName = quotation.Lead?.Client?.Name ?? "Cliente no especificado",
            ClientIdentification = identification,
            ClientIdentificationType = identificationType,
            ProjectName = quotation.ProjectName, // Usa la propiedad calculada
            TotalPrice = quotation.TotalPrice,
            FinalPrice = quotation.FinalPrice,
            AmountFinanced = quotation.AmountFinanced,
            ExchangeRate = quotation.ExchangeRate,
            BlockName = quotation.BlockName, // Usa la propiedad calculada
            LotNumber = quotation.LotNumber, // Usa la propiedad calculada
            AreaAtQuotation = quotation.AreaAtQuotation, // Usa el área histórica
            Status = quotation.Status,
            StatusText = GetStatusText(quotation.Status),
            ValidUntil = quotation.ValidUntil,
            QuotationDate = quotation.QuotationDate,
            Currency = quotation.Currency,
            CreatedAt = quotation.CreatedAt,
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
