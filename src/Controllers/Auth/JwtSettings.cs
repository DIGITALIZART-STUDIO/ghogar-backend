public class JwtSettings
{
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public required string SecretKey { get; set; }
    public required int ExpirationSeconds { get; set; } = 360;
    public required int RefreshExpirationSeconds { get; set; } = 43200;
}
