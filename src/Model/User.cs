using Microsoft.AspNetCore.Identity;

namespace GestionHogar.Model;

public class User : IdentityUser<Guid>, IEntity
{
    public DateTime? LastLogin { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}
