namespace GestionHogar.Controllers.Dtos;

public class ImportResult
{
    public int SuccessCount { get; set; }
    public int ClientsCreated { get; set; }
    public int ClientsExisting { get; set; }
    public int LeadsCreated { get; set; }
    public List<string> Errors { get; set; }
}
