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
        // Quitar tildes y caracteres especiales
        var normalized = name.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in normalized)
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        var noAccents = sb.ToString().Normalize(System.Text.NormalizationForm.FormC);

        // Reemplazar espacios por guion bajo y quitar caracteres no alfanuméricos (excepto guion bajo)
        var username = new string(
            noAccents.Replace(' ', '_').Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray()
        );

        // Convertir a minúsculas
        return username.ToLower();
    }
}
