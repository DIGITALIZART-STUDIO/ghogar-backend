using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;
using GestionHogar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Controllers;

/// <summary>
/// Controlador optimizado para paginación con parámetros avanzados y validaciones robustas
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OptimizedPaginationController : ControllerBase
{
    private readonly PaginationService _paginationService;
    private readonly AdvancedPaginationService _advancedPaginationService;
    private readonly PaginationCacheService _cacheService;
    private readonly ILogger<OptimizedPaginationController> _logger;

    public OptimizedPaginationController(
        PaginationService paginationService,
        AdvancedPaginationService advancedPaginationService,
        PaginationCacheService cacheService,
        ILogger<OptimizedPaginationController> logger
    )
    {
        _paginationService = paginationService;
        _advancedPaginationService = advancedPaginationService;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <summary>
    /// Pagina una consulta con parámetros optimizados y validaciones robustas
    /// </summary>
    /// <param name="request">Parámetros de paginación optimizados</param>
    /// <returns>Respuesta paginada optimizada</returns>
    [HttpPost("paginate")]
    [ProducesResponseType(typeof(OptimizedPaginatedResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<OptimizedPaginatedResponse<object>>> PaginateOptimized(
        [FromBody] OptimizedPaginationRequest request
    )
    {
        try
        {
            // Validar parámetros
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validar parámetros adicionales
            var validationResult = ValidatePaginationRequest(request);
            if (!validationResult.IsValid)
            {
                return BadRequest(validationResult.Errors);
            }

            _logger.LogInformation(
                "Iniciando paginación optimizada: página {Page}, pageSize {PageSize}, filtros: {FilterCount}",
                request.Page,
                request.PageSize,
                request.Filters?.Count ?? 0
            );

            // Ejecutar paginación basada en el tipo de consulta
            var result = await ExecuteOptimizedPagination(request);

            _logger.LogInformation(
                "Paginación optimizada completada: {Total} elementos, tiempo: {ExecutionTime}ms",
                result.Meta.Total,
                result.Meta.ExecutionTimeMs
            );

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en paginación optimizada");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene estadísticas de performance de paginación
    /// </summary>
    [HttpGet("performance-stats")]
    [ProducesResponseType(typeof(PaginationPerformanceStats), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginationPerformanceStats>> GetPerformanceStats(
        [FromQuery] string entityType,
        [FromQuery] string? filterHash = null
    )
    {
        try
        {
            // Obtener estadísticas de performance
            var stats = await GetPerformanceStatsForEntity(entityType, filterHash);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo estadísticas de performance");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene recomendaciones de índices para una entidad
    /// </summary>
    [HttpGet("index-recommendations/{entityType}")]
    [ProducesResponseType(typeof(List<IndexRecommendation>), StatusCodes.Status200OK)]
    public ActionResult<List<IndexRecommendation>> GetIndexRecommendations(string entityType)
    {
        try
        {
            var recommendations = GetRecommendationsForEntityType(entityType);
            return Ok(recommendations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo recomendaciones de índices");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Invalida caché de paginación
    /// </summary>
    [HttpPost("invalidate-cache")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult InvalidateCache([FromBody] InvalidateCacheRequest request)
    {
        try
        {
            if (request.Pattern != null)
            {
                _cacheService.InvalidateByPattern(request.Pattern);
            }
            else if (request.Keys != null)
            {
                _cacheService.InvalidateMany(request.Keys);
            }

            _logger.LogInformation(
                "Caché invalidado: {Pattern}",
                request.Pattern ?? string.Join(", ", request.Keys ?? new List<string>())
            );
            return Ok(new { message = "Caché invalidado exitosamente" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidando caché");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Ejecuta paginación optimizada basada en los parámetros
    /// </summary>
    private async Task<OptimizedPaginatedResponse<object>> ExecuteOptimizedPagination(
        OptimizedPaginationRequest request
    )
    {
        // Esta es una implementación simplificada
        // En producción, esto construiría la consulta real basada en los parámetros

        var mockQuery = CreateMockQuery(request.EntityType);
        var mockData = CreateMockData(request.EntityType, request.Page, request.PageSize);

        // Simular metadatos optimizados
        var meta = new OptimizedPaginationMetadata
        {
            Total = 1000, // Mock total
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = (int)Math.Ceiling(1000.0 / request.PageSize),
            HasNext = request.Page < (int)Math.Ceiling(1000.0 / request.PageSize),
            HasPrevious = request.Page > 1,
            CurrentPageCount = mockData.Count,
            StartIndex = ((request.Page - 1) * request.PageSize) + 1,
            EndIndex = Math.Min(request.Page * request.PageSize, 1000),
            ExecutionTimeMs = 50, // Mock execution time
            CacheHit = false,
            QueryComplexity = CalculateQueryComplexity(request),
            RecommendedPageSize = CalculateRecommendedPageSize(1000),
            HasOptimizedIndexes = true,
        };

        return new OptimizedPaginatedResponse<object>
        {
            Data = mockData,
            Meta = meta,
            Performance = new PaginationPerformanceInfo
            {
                QueryExecutionTimeMs = 45,
                CountExecutionTimeMs = 5,
                CacheHitRate = 0.85,
                IndexUtilization = 0.95,
            },
        };
    }

    /// <summary>
    /// Valida parámetros de paginación
    /// </summary>
    private ValidationResult ValidatePaginationRequest(OptimizedPaginationRequest request)
    {
        var errors = new List<string>();

        // Validar página
        if (request.Page < 1)
        {
            errors.Add("La página debe ser mayor a 0");
        }

        // Validar pageSize
        if (request.PageSize < 1 || request.PageSize > 1000)
        {
            errors.Add("El tamaño de página debe estar entre 1 y 1000");
        }

        // Validar entityType
        if (string.IsNullOrEmpty(request.EntityType))
        {
            errors.Add("El tipo de entidad es requerido");
        }

        // Validar filtros si existen
        if (request.Filters != null)
        {
            foreach (var filter in request.Filters)
            {
                if (string.IsNullOrEmpty(filter.Field))
                {
                    errors.Add("El campo del filtro no puede estar vacío");
                }
            }
        }

        return new ValidationResult { IsValid = errors.Count == 0, Errors = errors };
    }

    /// <summary>
    /// Calcula complejidad de la consulta
    /// </summary>
    private int CalculateQueryComplexity(OptimizedPaginationRequest request)
    {
        var complexity = 1;

        if (request.Filters?.Count > 0)
            complexity += request.Filters.Count;
        if (request.OrderBy?.Count > 0)
            complexity += request.OrderBy.Count;
        if (request.Includes?.Count > 0)
            complexity += request.Includes.Count * 2;

        return complexity;
    }

    /// <summary>
    /// Calcula tamaño de página recomendado
    /// </summary>
    private int CalculateRecommendedPageSize(int totalCount)
    {
        return totalCount switch
        {
            < 100 => 10,
            < 1000 => 25,
            < 10000 => 50,
            _ => 100,
        };
    }

    /// <summary>
    /// Crea consulta mock para testing
    /// </summary>
    private IQueryable<object> CreateMockQuery(string entityType)
    {
        // Implementación mock - en producción esto construiría la consulta real
        return new List<object>().AsQueryable();
    }

    /// <summary>
    /// Crea datos mock para testing
    /// </summary>
    private List<object> CreateMockData(string entityType, int page, int pageSize)
    {
        // Implementación mock - en producción esto obtendría datos reales
        return new List<object>();
    }

    /// <summary>
    /// Obtiene estadísticas de performance para una entidad
    /// </summary>
    private async Task<PaginationPerformanceStats> GetPerformanceStatsForEntity(
        string entityType,
        string? filterHash
    )
    {
        // Implementación mock - en producción esto obtendría estadísticas reales
        return new PaginationPerformanceStats
        {
            TotalRecords = 1000,
            QueryComplexity = 3,
            EstimatedPageCount = 100,
            CountExecutionTimeMs = 5,
            RecommendedPageSize = 25,
            HasIndexes = true,
        };
    }

    /// <summary>
    /// Obtiene recomendaciones de índices para un tipo de entidad
    /// </summary>
    private List<IndexRecommendation> GetRecommendationsForEntityType(string entityType)
    {
        return entityType.ToLower() switch
        {
            "lead" => IndexRecommendations.GetRecommendationsForEntity<Lead>(),
            "client" => IndexRecommendations.GetRecommendationsForEntity<Model.Client>(),
            "reservation" => IndexRecommendations.GetRecommendationsForEntity<Reservation>(),
            "quotation" => IndexRecommendations.GetRecommendationsForEntity<Quotation>(),
            "user" => IndexRecommendations.GetRecommendationsForEntity<User>(),
            "project" => IndexRecommendations.GetRecommendationsForEntity<Project>(),
            _ => new List<IndexRecommendation>(),
        };
    }
}

/// <summary>
/// Solicitud de paginación optimizada
/// </summary>
public class OptimizedPaginationRequest
{
    /// <summary>
    /// Número de página (1-based)
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "La página debe ser mayor a 0")]
    public int Page { get; set; } = 1;

    /// <summary>
    /// Tamaño de página
    /// </summary>
    [Range(1, 1000, ErrorMessage = "El tamaño de página debe estar entre 1 y 1000")]
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// Tipo de entidad a paginar
    /// </summary>
    [Required(ErrorMessage = "El tipo de entidad es requerido")]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Filtros a aplicar
    /// </summary>
    public List<PaginationFilter>? Filters { get; set; }

    /// <summary>
    /// Campos de ordenamiento
    /// </summary>
    public List<PaginationOrderBy>? OrderBy { get; set; }

    /// <summary>
    /// Relaciones a incluir
    /// </summary>
    public List<string>? Includes { get; set; }

    /// <summary>
    /// Usar paginación basada en cursor
    /// </summary>
    public bool UseCursorPagination { get; set; } = false;

    /// <summary>
    /// Cursor para paginación basada en cursor
    /// </summary>
    public string? Cursor { get; set; }

    /// <summary>
    /// Usar caché
    /// </summary>
    public bool UseCache { get; set; } = true;

    /// <summary>
    /// TTL del caché en minutos
    /// </summary>
    [Range(1, 60, ErrorMessage = "El TTL del caché debe estar entre 1 y 60 minutos")]
    public int CacheTTLMinutes { get; set; } = 5;
}

/// <summary>
/// Filtro de paginación
/// </summary>
public class PaginationFilter
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = "eq";
    public object? Value { get; set; }
}

/// <summary>
/// Ordenamiento de paginación
/// </summary>
public class PaginationOrderBy
{
    public string Field { get; set; } = string.Empty;
    public string Direction { get; set; } = "asc";
}

/// <summary>
/// Respuesta paginada optimizada
/// </summary>
public class OptimizedPaginatedResponse<T>
{
    public List<T> Data { get; set; } = new();
    public OptimizedPaginationMetadata Meta { get; set; } = new();
    public PaginationPerformanceInfo Performance { get; set; } = new();
}

/// <summary>
/// Metadatos de paginación optimizados
/// </summary>
public class OptimizedPaginationMetadata : PaginationMetadata
{
    public bool CacheHit { get; set; }
    public int QueryComplexity { get; set; }
    public int RecommendedPageSize { get; set; }
    public bool HasOptimizedIndexes { get; set; }
}

/// <summary>
/// Información de performance de paginación
/// </summary>
public class PaginationPerformanceInfo
{
    public long QueryExecutionTimeMs { get; set; }
    public long CountExecutionTimeMs { get; set; }
    public double CacheHitRate { get; set; }
    public double IndexUtilization { get; set; }
}

/// <summary>
/// Solicitud de invalidación de caché
/// </summary>
public class InvalidateCacheRequest
{
    public string? Pattern { get; set; }
    public List<string>? Keys { get; set; }
}

/// <summary>
/// Resultado de validación
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}
