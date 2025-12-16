namespace GestionHogar.Model;

/// <summary>
/// Metadatos optimizados de paginación con validaciones y cálculos mejorados
/// </summary>
public class PaginationMetadata
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasNext { get; set; }
    public bool HasPrevious { get; set; }

    /// <summary>
    /// Número de elementos en la página actual
    /// </summary>
    public int CurrentPageCount { get; set; }

    /// <summary>
    /// Índice del primer elemento en la página actual (1-based)
    /// </summary>
    public int StartIndex { get; set; }

    /// <summary>
    /// Índice del último elemento en la página actual (1-based)
    /// </summary>
    public int EndIndex { get; set; }

    /// <summary>
    /// Tiempo de ejecución de la consulta en milisegundos
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// Indica si la página actual está vacía
    /// </summary>
    public bool IsEmpty => CurrentPageCount == 0;

    /// <summary>
    /// Indica si hay datos para mostrar
    /// </summary>
    public bool HasData => Total > 0;

    /// <summary>
    /// Crea metadatos de paginación con validaciones y cálculos optimizados
    /// </summary>
    public static PaginationMetadata Create(
        int total,
        int page,
        int pageSize,
        long executionTimeMs = 0
    )
    {
        // Validaciones de entrada
        if (page < 1)
            page = 1;
        if (pageSize < 1)
            pageSize = 10;
        if (total < 0)
            total = 0;

        // Cálculos optimizados
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling((double)total / pageSize);

        // Ajustar página si excede el total
        if (page > totalPages && totalPages > 0)
            page = totalPages;

        var startIndex = total == 0 ? 0 : ((page - 1) * pageSize) + 1;
        var endIndex = Math.Min(page * pageSize, total);
        var currentPageCount = total == 0 ? 0 : Math.Max(0, endIndex - startIndex + 1);

        return new PaginationMetadata
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            HasNext = page < totalPages,
            HasPrevious = page > 1,
            CurrentPageCount = currentPageCount,
            StartIndex = startIndex,
            EndIndex = endIndex,
            ExecutionTimeMs = executionTimeMs,
        };
    }
}
