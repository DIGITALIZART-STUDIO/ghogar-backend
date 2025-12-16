using System.ComponentModel.DataAnnotations;

namespace GestionHogar.Model;

/// <summary>
/// Representa un filtro para paginación
/// </summary>
public class PaginationFilter
{
    /// <summary>
    /// Campo a filtrar
    /// </summary>
    [Required(ErrorMessage = "El campo es requerido")]
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Operador de comparación
    /// </summary>
    [Required(ErrorMessage = "El operador es requerido")]
    public string Operator { get; set; } = string.Empty;

    /// <summary>
    /// Valor a comparar
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Operador lógico para combinar con otros filtros (AND, OR)
    /// </summary>
    public string? LogicalOperator { get; set; } = "AND";
}
