using GestionHogar.Model;

namespace GestionHogar.Controllers.Dtos;

public class ContactResultDto
{
    public Guid ClientId { get; set; }
    public Guid LeadId { get; set; }
    public string LeadCode { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;

    // Información sobre qué se creó o encontró
    public ContactProcessInfo ProcessInfo { get; set; } = new();

    // Mensaje de éxito
    public string Message { get; set; } = string.Empty;
}

public class ContactProcessInfo
{
    public bool ClientExisted { get; set; }
    public bool ClientCreated { get; set; }
    public bool LeadCreated { get; set; }
}
