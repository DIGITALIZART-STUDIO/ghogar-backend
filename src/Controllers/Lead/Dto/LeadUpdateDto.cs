using GestionHogar.Model;

namespace GestionHogar.Controllers.Dtos;

public class LeadUpdateDto
{
    public Guid? ClientId { get; set; }
    public Guid? AssignedToId { get; set; }
    public LeadStatus? Status { get; set; }
    public string? Procedency { get; set; }
}
