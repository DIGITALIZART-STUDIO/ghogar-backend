using System.Collections.Generic;

namespace GestionHogar.Model;

/// <summary>
/// Respuesta paginada optimizada con metadatos enriquecidos y utilidades
/// </summary>
public class PaginatedResponseV2<T>
{
    public List<T> Data { get; set; } = new();
    public PaginationMetadata Meta { get; set; } = new();

    /// <summary>
    /// Indica si la respuesta tiene datos
    /// </summary>
    public bool HasData => Data.Count > 0;

    /// <summary>
    /// Indica si la respuesta está vacía
    /// </summary>
    public bool IsEmpty => Data.Count == 0;

    /// <summary>
    /// Número de elementos en la página actual
    /// </summary>
    public int Count => Data.Count;

    /// <summary>
    /// Crea una respuesta paginada vacía
    /// </summary>
    public static PaginatedResponseV2<T> Empty(int page = 1, int pageSize = 10)
    {
        return new PaginatedResponseV2<T>
        {
            Data = new List<T>(),
            Meta = PaginationMetadata.Create(0, page, pageSize),
        };
    }

    /// <summary>
    /// Crea una respuesta paginada con datos
    /// </summary>
    public static PaginatedResponseV2<T> Create(
        List<T> data,
        int total,
        int page,
        int pageSize,
        long executionTimeMs = 0
    )
    {
        return new PaginatedResponseV2<T>
        {
            Data = data ?? new List<T>(),
            Meta = PaginationMetadata.Create(total, page, pageSize, executionTimeMs),
        };
    }

    /// <summary>
    /// Crea una respuesta paginada desde un IQueryable
    /// </summary>
    public static async Task<PaginatedResponseV2<T>> CreateAsync(
        IQueryable<T> query,
        int page,
        int pageSize,
        Services.PaginationService paginationService
    )
    {
        return await paginationService.PaginateAsync(query, page, pageSize);
    }

    /// <summary>
    /// Convierte los datos a un tipo diferente manteniendo los metadatos
    /// </summary>
    public PaginatedResponseV2<TOutput> Map<TOutput>(Func<T, TOutput> mapper)
    {
        var mappedData = Data.Select(mapper).ToList();
        return new PaginatedResponseV2<TOutput> { Data = mappedData, Meta = Meta };
    }

    /// <summary>
    /// Filtra los datos manteniendo los metadatos originales
    /// </summary>
    public PaginatedResponseV2<T> Filter(Func<T, bool> predicate)
    {
        var filteredData = Data.Where(predicate).ToList();
        return new PaginatedResponseV2<T> { Data = filteredData, Meta = Meta };
    }
}
