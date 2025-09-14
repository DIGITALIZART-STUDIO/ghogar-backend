using GestionHogar.Model;

namespace GestionHogar.Controllers.Dtos;

public class UserSummaryDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public List<string> Roles { get; set; } = new List<string>();
}
