using System;
using System.Text.Json.Serialization;
using GestionHogar.Model;

namespace GestionHogar.Dtos;

public class QuotationSummaryDTO
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string ClientName { get; set; } = null!;
    public string? ClientIdentification { get; set; }
    public string? ClientIdentificationType { get; set; }
    public string ProjectName { get; set; } = null!;
    public decimal TotalPrice { get; set; }
    public decimal FinalPrice { get; set; }
    public string BlockName { get; set; } = null!; // Cambiado de Block a BlockName
    public string LotNumber { get; set; } = null!;
    public decimal AreaAtQuotation { get; set; } // Cambiado de Area a AreaAtQuotation

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public QuotationStatus Status { get; set; }
    public string StatusText { get; set; } = string.Empty;

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
            ClientName = quotation.Lead?.Client?.Name ?? "Cliente no especificado",
            ClientIdentification = identification,
            ClientIdentificationType = identificationType,
            ProjectName = quotation.ProjectName, // Usa la propiedad calculada
            TotalPrice = quotation.TotalPrice,
            FinalPrice = quotation.FinalPrice,
            BlockName = quotation.BlockName, // Usa la propiedad calculada
            LotNumber = quotation.LotNumber, // Usa la propiedad calculada
            AreaAtQuotation = quotation.AreaAtQuotation, // Usa el área histórica
            Status = quotation.Status,
            StatusText = GetStatusText(quotation.Status),
            ValidUntil = quotation.ValidUntil,
            QuotationDate = quotation.QuotationDate,
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
