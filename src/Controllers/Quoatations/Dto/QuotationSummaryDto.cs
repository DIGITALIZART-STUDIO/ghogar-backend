using System;
using GestionHogar.Model;

namespace GestionHogar.Dtos;

public class QuotationSummaryDTO
{
    // Identificador único
    public Guid Id { get; set; }

    // Código de la cotización (ej: COT-2025-00001)
    public string Code { get; set; } = null!;

    // Información del cliente/lead
    public string ClientName { get; set; } = null!;

    // DNI o RUC según el tipo de cliente
    public string? ClientIdentification { get; set; } // DNI si es persona natural, RUC si es jurídico
    public string? ClientIdentificationType { get; set; } // "DNI" o "RUC"

    // Información del proyecto
    public string ProjectName { get; set; } = null!;

    // Información financiera principal
    public decimal TotalPrice { get; set; }
    public decimal FinalPrice { get; set; }

    // Información del lote
    public string Block { get; set; } = null!;
    public string LotNumber { get; set; } = null!;
    public decimal Area { get; set; }

    // Estado de la cotización
    public string Status { get; set; } = null!;

    public string QuotationDate { get; set; } = null!;

    // Fechas importantes
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
            else // ClientType.Juridico
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
            ProjectName = quotation.ProjectName,
            TotalPrice = quotation.TotalPrice,
            FinalPrice = quotation.FinalPrice,
            Block = quotation.Block,
            LotNumber = quotation.LotNumber,
            Area = quotation.Area,
            Status = quotation.Status.ToString(),
            ValidUntil = quotation.ValidUntil,
            QuotationDate = quotation.QuotationDate,
            CreatedAt = quotation.CreatedAt,
        };
    }
}
