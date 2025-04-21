using GestionHogar.Model;

namespace GestionHogar.Controllers;

public class UserGetDTO
{
    public required User User { get; set; }
    public required IList<string> Roles { get; set; }
}
