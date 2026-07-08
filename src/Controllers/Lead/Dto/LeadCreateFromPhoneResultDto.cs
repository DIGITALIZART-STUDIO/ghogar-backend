using GestionHogar.Model;

namespace GestionHogar.Controllers.Dtos;

public class LeadCreateFromPhoneResultDto
{
    public required Lead Lead { get; set; }

    public Guid ClientId { get; set; }

    public bool ClientCreated { get; set; }

    public bool ClientExisted { get; set; }
}
