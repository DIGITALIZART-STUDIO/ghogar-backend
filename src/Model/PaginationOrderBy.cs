using System.ComponentModel.DataAnnotations;

namespace GestionHogar.Model;

/// <summary>
/// Representa un criterio de ordenamiento para paginación
/// </summary>
public class PaginationOrderBy
{
    /// <summary>
    /// Campo por el cual ordenar
    /// </summary>
    [Required(ErrorMessage = "El campo de ordenamiento es requerido")]
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Dirección del ordenamiento (asc, desc)
    /// </summary>
    [Required(ErrorMessage = "La dirección de ordenamiento es requerida")]
    public string Direction { get; set; } = "asc";
}
