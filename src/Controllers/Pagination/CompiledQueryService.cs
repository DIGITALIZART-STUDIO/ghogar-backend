using System.Linq.Expressions;
using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Services;

/// <summary>
/// Servicio de consultas compiladas para optimizar performance de paginación
/// </summary>
public class CompiledQueryService
{
    private readonly ILogger<CompiledQueryService>? _logger;
    private readonly Dictionary<string, Delegate> _compiledQueries;

    public CompiledQueryService(ILogger<CompiledQueryService>? logger = null)
    {
        _logger = logger;
        _compiledQueries = new Dictionary<string, Delegate>();
        InitializeCompiledQueries();
    }

    /// <summary>
    /// Inicializa consultas compiladas para entidades principales
    /// </summary>
    private void InitializeCompiledQueries()
    {
        _logger?.LogInformation(
            "Inicializando consultas compiladas para optimización de paginación"
        );

        // Consultas compiladas para Leads
        InitializeLeadQueries();

        // Consultas compiladas para Clients
        InitializeClientQueries();

        // Consultas compiladas para Reservations
        InitializeReservationQueries();

        // Consultas compiladas para Quotations
        InitializeQuotationQueries();

        // Consultas compiladas para Users
        InitializeUserQueries();

        // Consultas compiladas para Projects
        InitializeProjectQueries();

        _logger?.LogInformation(
            "Consultas compiladas inicializadas: {Count} consultas",
            _compiledQueries.Count
        );
    }

    /// <summary>
    /// Inicializa consultas compiladas para Leads
    /// </summary>
    private void InitializeLeadQueries()
    {
        // Consulta para obtener leads activos paginados
        var getActiveLeadsQuery = EF.CompileAsyncQuery(
            (DatabaseContext context, int skip, int take) =>
                context
                    .Leads.Where(l => l.IsActive)
                    .OrderByDescending(l => l.CreatedAt)
                    .Skip(skip)
                    .Take(take)
        );

        _compiledQueries["GetActiveLeads"] = getActiveLeadsQuery;

        // Consulta para contar leads activos
        var countActiveLeadsQuery = EF.CompileAsyncQuery(
            (DatabaseContext context) => context.Leads.Count(l => l.IsActive)
        );

        _compiledQueries["CountActiveLeads"] = countActiveLeadsQuery;

        // Consulta para obtener leads por estado
        var getLeadsByStatusQuery = EF.CompileAsyncQuery(
            (DatabaseContext context, string status, int skip, int take) =>
                context
                    .Leads.Where(l => l.IsActive && l.Status.ToString() == status)
                    .OrderByDescending(l => l.CreatedAt)
                    .Skip(skip)
                    .Take(take)
        );

        _compiledQueries["GetLeadsByStatus"] = getLeadsByStatusQuery;

        // Consulta para contar leads por estado
        var countLeadsByStatusQuery = EF.CompileAsyncQuery(
            (DatabaseContext context, string status) =>
                context.Leads.Count(l => l.IsActive && l.Status.ToString() == status)
        );

        _compiledQueries["CountLeadsByStatus"] = countLeadsByStatusQuery;
    }

    /// <summary>
    /// Inicializa consultas compiladas para Clients
    /// </summary>
    private void InitializeClientQueries()
    {
        // Consulta para obtener clientes activos paginados
        var getActiveClientsQuery = EF.CompileAsyncQuery(
            (DatabaseContext context, int skip, int take) =>
                context
                    .Clients.Where(c => c.IsActive)
                    .OrderByDescending(c => c.CreatedAt)
                    .Skip(skip)
                    .Take(take)
        );

        _compiledQueries["GetActiveClients"] = getActiveClientsQuery;

        // Consulta para contar clientes activos
        var countActiveClientsQuery = EF.CompileAsyncQuery(
            (DatabaseContext context) => context.Clients.Count(c => c.IsActive)
        );

        _compiledQueries["CountActiveClients"] = countActiveClientsQuery;

        // Consulta para buscar clientes por nombre
        var searchClientsByNameQuery = EF.CompileAsyncQuery(
            (DatabaseContext context, string name, int skip, int take) =>
                context
                    .Clients.Where(c => c.IsActive && c.Name.Contains(name))
                    .OrderBy(c => c.Name)
                    .Skip(skip)
                    .Take(take)
        );

        _compiledQueries["SearchClientsByName"] = searchClientsByNameQuery;
    }

    /// <summary>
    /// Inicializa consultas compiladas para Reservations
    /// </summary>
    private void InitializeReservationQueries()
    {
        // Consulta para obtener reservas activas paginadas
        var getActiveReservationsQuery = EF.CompileAsyncQuery(
            (DatabaseContext context, int skip, int take) =>
                context
                    .Reservations.Where(r => r.IsActive)
                    .OrderByDescending(r => r.CreatedAt)
                    .Skip(skip)
                    .Take(take)
        );

        _compiledQueries["GetActiveReservations"] = getActiveReservationsQuery;

        // Consulta para contar reservas activas
        var countActiveReservationsQuery = EF.CompileAsyncQuery(
            (DatabaseContext context) => context.Reservations.Count(r => r.IsActive)
        );

        _compiledQueries["CountActiveReservations"] = countActiveReservationsQuery;

        // Consulta para obtener reservas por cliente
        var getReservationsByClientQuery = EF.CompileAsyncQuery(
            (DatabaseContext context, Guid clientId, int skip, int take) =>
                context
                    .Reservations.Where(r => r.IsActive && r.ClientId == clientId)
                    .OrderByDescending(r => r.CreatedAt)
                    .Skip(skip)
                    .Take(take)
        );

        _compiledQueries["GetReservationsByClient"] = getReservationsByClientQuery;
    }

    /// <summary>
    /// Inicializa consultas compiladas para Quotations
    /// </summary>
    private void InitializeQuotationQueries()
    {
        // Consulta para obtener cotizaciones activas paginadas
        var getActiveQuotationsQuery = EF.CompileAsyncQuery(
            (DatabaseContext context, int skip, int take) =>
                context
                    .Quotations.Where(q => q.Status != QuotationStatus.CANCELED)
                    .OrderByDescending(q => q.CreatedAt)
                    .Skip(skip)
                    .Take(take)
        );

        _compiledQueries["GetActiveQuotations"] = getActiveQuotationsQuery;

        // Consulta para contar cotizaciones activas
        var countActiveQuotationsQuery = EF.CompileAsyncQuery(
            (DatabaseContext context) =>
                context.Quotations.Count(q => q.Status != QuotationStatus.CANCELED)
        );

        _compiledQueries["CountActiveQuotations"] = countActiveQuotationsQuery;

        // Consulta para obtener cotizaciones por lead
        var getQuotationsByLeadQuery = EF.CompileAsyncQuery(
            (DatabaseContext context, Guid leadId, int skip, int take) =>
                context
                    .Quotations.Where(q =>
                        q.Status != QuotationStatus.CANCELED && q.LeadId == leadId
                    )
                    .OrderByDescending(q => q.CreatedAt)
                    .Skip(skip)
                    .Take(take)
        );

        _compiledQueries["GetQuotationsByLead"] = getQuotationsByLeadQuery;
    }

    /// <summary>
    /// Inicializa consultas compiladas para Users
    /// </summary>
    private void InitializeUserQueries()
    {
        // Consulta para obtener usuarios activos paginados
        var getActiveUsersQuery = EF.CompileAsyncQuery(
            (DatabaseContext context, int skip, int take) =>
                context.Users.Where(u => u.IsActive).OrderBy(u => u.Name).Skip(skip).Take(take)
        );

        _compiledQueries["GetActiveUsers"] = getActiveUsersQuery;

        // Consulta para contar usuarios activos
        var countActiveUsersQuery = EF.CompileAsyncQuery(
            (DatabaseContext context) => context.Users.Count(u => u.IsActive)
        );

        _compiledQueries["CountActiveUsers"] = countActiveUsersQuery;
    }

    /// <summary>
    /// Inicializa consultas compiladas para Projects
    /// </summary>
    private void InitializeProjectQueries()
    {
        // Consulta para obtener proyectos activos paginados
        var getActiveProjectsQuery = EF.CompileAsyncQuery(
            (DatabaseContext context, int skip, int take) =>
                context.Projects.Where(p => p.IsActive).OrderBy(p => p.Name).Skip(skip).Take(take)
        );

        _compiledQueries["GetActiveProjects"] = getActiveProjectsQuery;

        // Consulta para contar proyectos activos
        var countActiveProjectsQuery = EF.CompileAsyncQuery(
            (DatabaseContext context) => context.Projects.Count(p => p.IsActive)
        );

        _compiledQueries["CountActiveProjects"] = countActiveProjectsQuery;
    }

    /// <summary>
    /// Ejecuta una consulta compilada
    /// </summary>
    public async Task<TResult> ExecuteCompiledQueryAsync<TResult>(
        string queryName,
        DatabaseContext context,
        params object[] parameters
    )
    {
        if (!_compiledQueries.TryGetValue(queryName, out var compiledQuery))
        {
            throw new ArgumentException($"Consulta compilada no encontrada: {queryName}");
        }

        try
        {
            _logger?.LogDebug("Ejecutando consulta compilada: {QueryName}", queryName);

            var task =
                (Task<TResult>)
                    compiledQuery.DynamicInvoke(
                        new object[] { context }
                            .Concat(parameters)
                            .ToArray()
                    );

            return await task;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error ejecutando consulta compilada: {QueryName}", queryName);
            throw;
        }
    }

    /// <summary>
    /// Obtiene lista de consultas compiladas disponibles
    /// </summary>
    public List<string> GetAvailableQueries()
    {
        return _compiledQueries.Keys.ToList();
    }

    /// <summary>
    /// Verifica si una consulta compilada existe
    /// </summary>
    public bool HasCompiledQuery(string queryName)
    {
        return _compiledQueries.ContainsKey(queryName);
    }

    /// <summary>
    /// Obtiene estadísticas de consultas compiladas
    /// </summary>
    public CompiledQueryStats GetStats()
    {
        return new CompiledQueryStats
        {
            TotalQueries = _compiledQueries.Count,
            QueryNames = _compiledQueries.Keys.ToList(),
            LastUpdated = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Limpia consultas compiladas (útil para testing)
    /// </summary>
    public void ClearCompiledQueries()
    {
        _compiledQueries.Clear();
        _logger?.LogInformation("Consultas compiladas limpiadas");
    }
}

/// <summary>
/// Estadísticas de consultas compiladas
/// </summary>
public class CompiledQueryStats
{
    public int TotalQueries { get; set; }
    public List<string> QueryNames { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Servicio de consultas compiladas específicas para paginación
/// </summary>
public class PaginationCompiledQueryService
{
    private readonly CompiledQueryService _compiledQueryService;
    private readonly ILogger<PaginationCompiledQueryService>? _logger;

    public PaginationCompiledQueryService(
        CompiledQueryService compiledQueryService,
        ILogger<PaginationCompiledQueryService>? logger = null
    )
    {
        _compiledQueryService = compiledQueryService;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta paginación usando consultas compiladas para Leads
    /// </summary>
    public async Task<PaginatedResponseV2<Lead>> GetLeadsPaginatedAsync(
        DatabaseContext context,
        int page,
        int pageSize,
        string? status = null
    )
    {
        var skip = (page - 1) * pageSize;
        var queryName = string.IsNullOrEmpty(status) ? "GetActiveLeads" : "GetLeadsByStatus";
        var countQueryName = string.IsNullOrEmpty(status)
            ? "CountActiveLeads"
            : "CountLeadsByStatus";

        var parameters = string.IsNullOrEmpty(status)
            ? new object[] { skip, pageSize }
            : new object[] { status, skip, pageSize };

        var countParameters = string.IsNullOrEmpty(status)
            ? new object[0]
            : new object[] { status };

        var dataTask = _compiledQueryService.ExecuteCompiledQueryAsync<List<Lead>>(
            queryName,
            context,
            parameters
        );

        var countTask = _compiledQueryService.ExecuteCompiledQueryAsync<int>(
            countQueryName,
            context,
            countParameters
        );

        await Task.WhenAll(dataTask, countTask);

        var data = await dataTask;
        var total = await countTask;

        return PaginatedResponseV2<Lead>.Create(data, total, page, pageSize);
    }

    /// <summary>
    /// Ejecuta paginación usando consultas compiladas para Clients
    /// </summary>
    public async Task<PaginatedResponseV2<Client>> GetClientsPaginatedAsync(
        DatabaseContext context,
        int page,
        int pageSize,
        string? searchName = null
    )
    {
        var skip = (page - 1) * pageSize;
        var queryName = string.IsNullOrEmpty(searchName)
            ? "GetActiveClients"
            : "SearchClientsByName";
        var countQueryName = "CountActiveClients";

        var parameters = string.IsNullOrEmpty(searchName)
            ? new object[] { skip, pageSize }
            : new object[] { searchName, skip, pageSize };

        var dataTask = _compiledQueryService.ExecuteCompiledQueryAsync<List<Client>>(
            queryName,
            context,
            parameters
        );

        var countTask = _compiledQueryService.ExecuteCompiledQueryAsync<int>(
            countQueryName,
            context
        );

        await Task.WhenAll(dataTask, countTask);

        var data = await dataTask;
        var total = await countTask;

        return PaginatedResponseV2<Client>.Create(data, total, page, pageSize);
    }

    /// <summary>
    /// Ejecuta paginación usando consultas compiladas para Reservations
    /// </summary>
    public async Task<PaginatedResponseV2<Reservation>> GetReservationsPaginatedAsync(
        DatabaseContext context,
        int page,
        int pageSize,
        Guid? clientId = null
    )
    {
        var skip = (page - 1) * pageSize;
        var queryName = clientId.HasValue ? "GetReservationsByClient" : "GetActiveReservations";
        var countQueryName = "CountActiveReservations";

        var parameters = clientId.HasValue
            ? new object[] { clientId.Value, skip, pageSize }
            : new object[] { skip, pageSize };

        var dataTask = _compiledQueryService.ExecuteCompiledQueryAsync<List<Reservation>>(
            queryName,
            context,
            parameters
        );

        var countTask = _compiledQueryService.ExecuteCompiledQueryAsync<int>(
            countQueryName,
            context
        );

        await Task.WhenAll(dataTask, countTask);

        var data = await dataTask;
        var total = await countTask;

        return PaginatedResponseV2<Reservation>.Create(data, total, page, pageSize);
    }
}
