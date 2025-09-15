using System.Diagnostics;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Services;

/// <summary>
/// Servicio de paginación optimizado con consultas eficientes y validaciones robustas
/// </summary>
public class PaginationService
{
    private readonly ILogger<PaginationService>? _logger;

    public PaginationService(ILogger<PaginationService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Pagina una consulta de forma optimizada con validaciones y medición de performance
    /// </summary>
    public async Task<PaginatedResponseV2<T>> PaginateAsync<T>(
        IQueryable<T> query,
        int page,
        int pageSize
    )
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Validar parámetros de entrada
            var validatedParams = ValidatePaginationParams(page, pageSize);
            page = validatedParams.Page;
            pageSize = validatedParams.PageSize;

            // Ejecutar conteo y paginación de forma optimizada
            var (total, data) = await ExecuteOptimizedPagination(query, page, pageSize);

            stopwatch.Stop();

            // Crear metadatos optimizados
            var meta = PaginationMetadata.Create(
                total,
                page,
                pageSize,
                stopwatch.ElapsedMilliseconds
            );

            _logger?.LogDebug(
                "Paginación completada: {Total} elementos, página {Page}/{TotalPages}, tiempo: {ExecutionTime}ms",
                total,
                page,
                meta.TotalPages,
                stopwatch.ElapsedMilliseconds
            );

            return new PaginatedResponseV2<T> { Data = data, Meta = meta };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogError(
                ex,
                "Error en paginación: página {Page}, pageSize {PageSize}, tiempo: {ExecutionTime}ms",
                page,
                pageSize,
                stopwatch.ElapsedMilliseconds
            );
            throw;
        }
    }

    /// <summary>
    /// Pagina usando parámetros validados
    /// </summary>
    public async Task<PaginatedResponseV2<T>> PaginateAsync<T>(
        IQueryable<T> query,
        PaginationParams paginationParams
    )
    {
        return await PaginateAsync(query, paginationParams.Page, paginationParams.PageSize);
    }

    /// <summary>
    /// Ejecuta paginación optimizada combinando conteo y datos cuando es posible
    /// </summary>
    private async Task<(int total, List<T> data)> ExecuteOptimizedPagination<T>(
        IQueryable<T> query,
        int page,
        int pageSize
    )
    {
        // Para consultas simples sin Include complejos, podemos optimizar
        if (IsSimpleQuery(query))
        {
            return await ExecuteSimplePagination(query, page, pageSize);
        }

        // Para consultas complejas, usar el método tradicional pero optimizado
        return await ExecuteComplexPagination(query, page, pageSize);
    }

    /// <summary>
    /// Determina si la consulta es simple (sin Include complejos)
    /// </summary>
    private static bool IsSimpleQuery<T>(IQueryable<T> query)
    {
        // Esta es una heurística simple. En un sistema más complejo,
        // podrías analizar el Expression Tree para determinar la complejidad
        var queryString = query.ToString();
        return !queryString.Contains("Include") && !queryString.Contains("ThenInclude");
    }

    /// <summary>
    /// Ejecuta paginación simple con optimizaciones
    /// </summary>
    private async Task<(int total, List<T> data)> ExecuteSimplePagination<T>(
        IQueryable<T> query,
        int page,
        int pageSize
    )
    {
        // Para evitar problemas de concurrencia con DbContext, ejecutar secuencialmente
        // aunque sea menos eficiente, es más seguro
        var total = await query.CountAsync();
        var data = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return (total, data);
    }

    /// <summary>
    /// Ejecuta paginación para consultas complejas
    /// </summary>
    private async Task<(int total, List<T> data)> ExecuteComplexPagination<T>(
        IQueryable<T> query,
        int page,
        int pageSize
    )
    {
        // Para consultas complejas, ejecutar secuencialmente para evitar problemas de memoria
        var total = await query.CountAsync();
        var data = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return (total, data);
    }

    /// <summary>
    /// Valida y normaliza los parámetros de paginación
    /// </summary>
    private static PaginationParams ValidatePaginationParams(int page, int pageSize)
    {
        return PaginationParams.Create(page, pageSize);
    }

    /// <summary>
    /// Obtiene solo el conteo total sin paginar (útil para validaciones previas)
    /// </summary>
    public async Task<int> GetTotalCountAsync<T>(IQueryable<T> query)
    {
        return await query.CountAsync();
    }

    /// <summary>
    /// Verifica si una página específica existe
    /// </summary>
    public async Task<bool> PageExistsAsync<T>(IQueryable<T> query, int page, int pageSize)
    {
        var validatedParams = ValidatePaginationParams(page, pageSize);
        var total = await GetTotalCountAsync(query);
        var totalPages =
            total == 0 ? 0 : (int)Math.Ceiling((double)total / validatedParams.PageSize);

        return validatedParams.Page <= totalPages;
    }
}
