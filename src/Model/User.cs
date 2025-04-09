using Microsoft.AspNetCore.Identity;

namespace GestionHogar.Model;

public class User : IdentityUser<Guid>, IEntity
{
    public required DateTime LastLogin { get; set; }
    public required bool IsActive { get; set; }
    public required bool MustChangePassword { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime ModifiedAt { get; set; }
}
