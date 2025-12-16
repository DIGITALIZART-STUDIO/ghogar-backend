using System.Linq.Expressions;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Services;

/// <summary>
/// Servicio para optimizar proyecciones en consultas de paginación
/// </summary>
public class ProjectionService
{
    private readonly ILogger<ProjectionService>? _logger;

    public ProjectionService(ILogger<ProjectionService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Crea una proyección optimizada para listado paginado
    /// </summary>
    public IQueryable<TProjection> CreateListProjection<TEntity, TProjection>(
        IQueryable<TEntity> query,
        Expression<Func<TEntity, TProjection>> projection
    )
    {
        _logger?.LogDebug(
            "Creando proyección optimizada para listado: {EntityType} -> {ProjectionType}",
            typeof(TEntity).Name,
            typeof(TProjection).Name
        );

        return query.Select(projection);
    }

    /// <summary>
    /// Crea proyección optimizada para entidades con relaciones
    /// </summary>
    public IQueryable<TProjection> CreateOptimizedProjection<TEntity, TProjection>(
        IQueryable<TEntity> query,
        Expression<Func<TEntity, TProjection>> projection,
        bool includeRelatedData = false
    )
        where TEntity : class
    {
        if (includeRelatedData)
        {
            // Para proyecciones con relaciones, usar Include selectivo
            return CreateProjectionWithIncludes(query, projection);
        }

        return query.Select(projection);
    }

    /// <summary>
    /// Crea proyección con includes selectivos
    /// </summary>
    private IQueryable<TProjection> CreateProjectionWithIncludes<TEntity, TProjection>(
        IQueryable<TEntity> query,
        Expression<Func<TEntity, TProjection>> projection
    )
        where TEntity : class
    {
        // Analizar la proyección para determinar qué includes son necesarios
        var includes = AnalyzeProjectionForIncludes(projection);

        foreach (var include in includes)
        {
            query = query.Include(include);
        }

        return query.Select(projection);
    }

    /// <summary>
    /// Analiza la proyección para determinar includes necesarios
    /// </summary>
    private List<string> AnalyzeProjectionForIncludes<TEntity, TProjection>(
        Expression<Func<TEntity, TProjection>> projection
    )
    {
        var includes = new List<string>();

        // Análisis básico de la expresión para encontrar propiedades de navegación
        var visitor = new NavigationPropertyVisitor();
        visitor.Visit(projection.Body);
        includes.AddRange(visitor.NavigationProperties);

        return includes;
    }

    /// <summary>
    /// Crea proyección para vista de resumen (solo campos esenciales)
    /// </summary>
    public IQueryable<TProjection> CreateSummaryProjection<TEntity, TProjection>(
        IQueryable<TEntity> query,
        Expression<Func<TEntity, TProjection>> projection
    )
    {
        _logger?.LogDebug(
            "Creando proyección de resumen optimizada: {EntityType}",
            typeof(TEntity).Name
        );

        // Para resúmenes, no incluir relaciones pesadas
        return query.Select(projection);
    }

    /// <summary>
    /// Crea proyección con campos calculados
    /// </summary>
    public IQueryable<TProjection> CreateCalculatedProjection<TEntity, TProjection>(
        IQueryable<TEntity> query,
        Expression<Func<TEntity, TProjection>> projection,
        Dictionary<string, Expression<Func<TEntity, object>>> calculatedFields
    )
    {
        // Implementación básica - en producción esto sería más sofisticado
        return query.Select(projection);
    }

    /// <summary>
    /// Optimiza consulta para paginación con proyección
    /// </summary>
    public async Task<PaginatedResponseV2<TProjection>> PaginateWithProjectionAsync<
        TEntity,
        TProjection
    >(
        IQueryable<TEntity> query,
        Expression<Func<TEntity, TProjection>> projection,
        int page,
        int pageSize,
        PaginationService paginationService,
        bool useOptimizedProjection = true
    )
        where TEntity : class
    {
        IQueryable<TProjection> projectedQuery;

        if (useOptimizedProjection)
        {
            projectedQuery = CreateOptimizedProjection(query, projection);
        }
        else
        {
            projectedQuery = query.Select(projection);
        }

        return await paginationService.PaginateAsync(projectedQuery, page, pageSize);
    }
}

/// <summary>
/// Visitor para analizar expresiones y encontrar propiedades de navegación
/// </summary>
internal class NavigationPropertyVisitor : ExpressionVisitor
{
    public List<string> NavigationProperties { get; } = new();

    protected override Expression VisitMember(MemberExpression node)
    {
        // Detectar propiedades de navegación (simplificado)
        if (IsNavigationProperty(node.Member))
        {
            NavigationProperties.Add(node.Member.Name);
        }

        return base.VisitMember(node);
    }

    private static bool IsNavigationProperty(System.Reflection.MemberInfo member)
    {
        // Lógica simplificada para detectar propiedades de navegación
        // En producción esto sería más sofisticado
        return member.Name.EndsWith("Id")
            || member.Name.Contains("Navigation")
            || member.Name.Contains("Collection");
    }
}

/// <summary>
/// Servicio para optimizar includes en consultas de paginación
/// </summary>
public class IncludeOptimizationService
{
    private readonly ILogger<IncludeOptimizationService>? _logger;

    public IncludeOptimizationService(ILogger<IncludeOptimizationService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Optimiza includes para consultas de paginación
    /// </summary>
    public IQueryable<T> OptimizeIncludesForPagination<T>(
        IQueryable<T> query,
        string[] requiredIncludes,
        bool useSelectiveLoading = true
    )
        where T : class
    {
        if (!useSelectiveLoading)
        {
            return query;
        }

        _logger?.LogDebug(
            "Optimizando includes para paginación: {IncludeCount} includes",
            requiredIncludes.Length
        );

        // Aplicar includes selectivos
        foreach (var include in requiredIncludes)
        {
            query = ApplySelectiveInclude(query, include);
        }

        return query;
    }

    /// <summary>
    /// Aplica include selectivo
    /// </summary>
    private IQueryable<T> ApplySelectiveInclude<T>(IQueryable<T> query, string includePath)
        where T : class
    {
        try
        {
            return query.Include(includePath);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "No se pudo aplicar include: {IncludePath}", includePath);
            return query;
        }
    }

    /// <summary>
    /// Determina includes necesarios basado en el tipo de vista
    /// </summary>
    public string[] GetRequiredIncludesForView<T>(string viewType)
    {
        return viewType.ToLower() switch
        {
            "list" => GetListIncludes<T>(),
            "summary" => GetSummaryIncludes<T>(),
            "detail" => GetDetailIncludes<T>(),
            _ => Array.Empty<string>(),
        };
    }

    private string[] GetListIncludes<T>()
    {
        // Includes mínimos para vista de lista
        return typeof(T).Name switch
        {
            "Lead" => new[] { "Client", "Project" },
            "Reservation" => new[] { "Client", "Quotation" },
            "Quotation" => new[] { "Lead", "Lot" },
            _ => Array.Empty<string>(),
        };
    }

    private string[] GetSummaryIncludes<T>()
    {
        // Includes mínimos para vista de resumen
        return Array.Empty<string>();
    }

    private string[] GetDetailIncludes<T>()
    {
        // Includes completos para vista de detalle
        return typeof(T).Name switch
        {
            "Lead" => new[] { "Client", "Project", "AssignedTo", "Referral" },
            "Reservation" => new[] { "Client", "Quotation", "Payments" },
            "Quotation" => new[] { "Lead", "Lot", "Advisor" },
            _ => Array.Empty<string>(),
        };
    }
}
