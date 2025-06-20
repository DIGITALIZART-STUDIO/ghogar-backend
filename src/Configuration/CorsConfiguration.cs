namespace GestionHogar.Configuration;

public class CorsConfiguration
{
    public required string[] AllowedOrigins { get; set; }
    public required int ExpirationSeconds { get; set; }
    public required int RefreshExpirationSeconds { get; set; }
    public string CookieName { get; set; } = "gestion_hogar_access_token";
    public string? CookieDomain { get; set; }
}
