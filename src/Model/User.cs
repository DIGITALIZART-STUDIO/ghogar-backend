using Microsoft.AspNetCore.Identity;

namespace GestionHogar.Model;

public class User : IdentityUser<Guid>, IEntity
{
    public DateTime? LastLogin { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    public static string CreateUsername(string name)
    {
        // Remove special characters and spaces
        var sanitizedName = new string(
            name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray()
        );

        return sanitizedName;
    }
}
