using System.ComponentModel.DataAnnotations;

namespace GestionHogar.Controllers.Dtos;

/// <summary>
/// DTO para la respuesta del tipo de cambio
/// </summary>
public class ExchangeRateDto
{
    /// <summary>
    /// Valor del tipo de cambio obtenido de SUNAT
    /// </summary>
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "El tipo de cambio debe ser mayor a 0")]
    public decimal ExchangeRate { get; set; }

    /// <summary>
    /// Fecha de obtención del tipo de cambio
    /// </summary>
    public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Fuente del tipo de cambio (SUNAT)
    /// </summary>
    public string Source { get; set; } = "SUNAT";

    /// <summary>
    /// Indica si la obtención fue exitosa
    /// </summary>
    public bool IsSuccess { get; set; } = true;

    /// <summary>
    /// Mensaje descriptivo del resultado
    /// </summary>
    public string Message { get; set; } = "Tipo de cambio obtenido exitosamente";
}
