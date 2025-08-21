namespace GestionHogar.Configuration;

public class CloudflareR2Configuration
{
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string ApiS3 { get; set; } = string.Empty;
    public string BucketName { get; set; } = "gestion-hogar";
    public string PublicUrlImage { get; set; } =
        "https://pub-2ec14747057247bba35d3b3cd6bd43a8.r2.dev";
}
