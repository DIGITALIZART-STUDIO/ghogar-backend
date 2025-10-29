using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionHogar.Model;

public class ApiPeruConsultation : IEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(20)]
    public string DocumentNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string DocumentType { get; set; } = string.Empty; // "RUC" o "DNI"

    [Required]
    public string ResponseData { get; set; } = string.Empty; // JSON serializado de la respuesta

    [MaxLength(500)]
    public string? CompanyName { get; set; }

    [MaxLength(500)]
    public string? PersonName { get; set; }

    [MaxLength(1000)]
    public string? Address { get; set; }

    [MaxLength(50)]
    public string? Status { get; set; }

    [MaxLength(50)]
    public string? Condition { get; set; }

    public DateTime ConsultedAt { get; set; } = DateTime.UtcNow;

    // Implementación de IEntity
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    // Método para serializar la respuesta
    public void SetResponseData<T>(T data)
    {
        ResponseData = System.Text.Json.JsonSerializer.Serialize(data);
    }

    // Método para deserializar la respuesta
    public T? GetResponseData<T>()
    {
        if (string.IsNullOrEmpty(ResponseData))
            return default;

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(ResponseData);
        }
        catch
        {
            return default;
        }
    }
}
