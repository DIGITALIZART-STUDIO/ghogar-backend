using GestionHogar.Model;

namespace GestionHogar.Controllers.Dtos;

public class ClientSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Dni { get; set; }
    public string? Ruc { get; set; }
}
