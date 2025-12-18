namespace GestionHogar.Model;

/// <summary>
/// Parámetros de paginación con validaciones y límites optimizados
/// </summary>
public class PaginationParams
{
    private int _page = 1;
    private int _pageSize = 10;

    /// <summary>
    /// Número de página (1-based). Mínimo: 1
    /// </summary>
    public int Page
    {
        get => _page;
        set => _page = Math.Max(1, value);
    }

    /// <summary>
    /// Tamaño de página. Mínimo: 1, Máximo: 100
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Clamp(value, 1, 100);
    }

    /// <summary>
    /// Límite máximo de elementos por página
    /// </summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// Tamaño de página por defecto
    /// </summary>
    public const int DefaultPageSize = 10;

    /// <summary>
    /// Página por defecto
    /// </summary>
    public const int DefaultPage = 1;

    /// <summary>
    /// Valida y normaliza los parámetros de paginación
    /// </summary>
    public void Validate()
    {
        _page = Math.Max(1, _page);
        _pageSize = Math.Clamp(_pageSize, 1, MaxPageSize);
    }

    /// <summary>
    /// Crea parámetros de paginación validados
    /// </summary>
    public static PaginationParams Create(int page = DefaultPage, int pageSize = DefaultPageSize)
    {
        var result = new PaginationParams { _page = page, _pageSize = pageSize };
        result.Validate();
        return result;
    }
}
