using System;
using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;

namespace GestionHogar.Controllers.Dtos;

public class LeadSummaryDto
{
    public Guid Id { get; set; }
    public ClientSummaryDto Client { get; set; } = null!;

    public static LeadSummaryDto FromEntity(Lead lead)
    {
        return new LeadSummaryDto
        {
            Id = lead.Id,
            Client = new ClientSummaryDto
            {
                Id = lead.Client!.Id,
                Name = lead.Client.Name,
                Dni = lead.Client.Dni,
                Ruc = lead.Client.Ruc,
            },
        };
    }
}
