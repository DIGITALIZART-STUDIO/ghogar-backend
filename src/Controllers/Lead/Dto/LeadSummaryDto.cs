using System;
using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;

namespace GestionHogar.Controllers.Dtos;

public class LeadSummaryDto
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!; // Código único del Lead, requerido para identificarlo
    public ClientSummaryDto Client { get; set; } = null!;
    public LeadStatus Status { get; set; }
    public DateTime ExpirationDate { get; set; }
    public string? ProjectName { get; set; }
    public int RecycleCount { get; set; }

    public static LeadSummaryDto FromEntity(Lead lead)
    {
        return new LeadSummaryDto
        {
            Id = lead.Id,
            Code = lead.Code,
            Client = new ClientSummaryDto
            {
                Id = lead.Client!.Id,
                Name = lead.Client.Name!,
                Dni = lead.Client.Dni,
                Ruc = lead.Client.Ruc,
                PhoneNumber = lead.Client.PhoneNumber,
            },
            Status = lead.Status,
            ExpirationDate = lead.ExpirationDate,
            ProjectName = lead.Project?.Name,
            RecycleCount = lead.RecycleCount,
        };
    }
}
