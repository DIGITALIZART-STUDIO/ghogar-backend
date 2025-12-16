using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Controllers;

/// <summary>
/// Recomendaciones de índices para optimizar consultas de paginación
/// </summary>
public static class IndexRecommendations
{
    /// <summary>
    /// Aplica índices recomendados para paginación en el modelo de datos
    /// </summary>
    public static void ApplyPaginationIndexes(ModelBuilder modelBuilder)
    {
        // Índices para Leads
        ApplyLeadIndexes(modelBuilder);

        // Índices para Clients
        ApplyClientIndexes(modelBuilder);

        // Índices para Reservations
        ApplyReservationIndexes(modelBuilder);

        // Índices para Quotations
        ApplyQuotationIndexes(modelBuilder);

        // Índices para Users
        ApplyUserIndexes(modelBuilder);

        // Índices para Projects
        ApplyProjectIndexes(modelBuilder);
    }

    /// <summary>
    /// Índices recomendados para Leads
    /// </summary>
    private static void ApplyLeadIndexes(ModelBuilder modelBuilder)
    {
        // Índice compuesto para paginación por fecha de creación
        modelBuilder
            .Entity<Lead>()
            .HasIndex(l => new { l.CreatedAt, l.Id })
            .HasDatabaseName("IX_Leads_CreatedAt_Id")
            .HasFilter("IsActive = 1");

        // Índice para filtros por estado
        modelBuilder
            .Entity<Lead>()
            .HasIndex(l => new { l.Status, l.CreatedAt })
            .HasDatabaseName("IX_Leads_Status_CreatedAt")
            .HasFilter("IsActive = 1");

        // Índice para filtros por usuario asignado
        modelBuilder
            .Entity<Lead>()
            .HasIndex(l => new { l.AssignedToId, l.CreatedAt })
            .HasDatabaseName("IX_Leads_AssignedToId_CreatedAt")
            .HasFilter("IsActive = 1");

        // Índice para filtros por proyecto
        modelBuilder
            .Entity<Lead>()
            .HasIndex(l => new { l.ProjectId, l.CreatedAt })
            .HasDatabaseName("IX_Leads_ProjectId_CreatedAt")
            .HasFilter("IsActive = 1");

        // Índice para búsquedas por cliente
        modelBuilder
            .Entity<Lead>()
            .HasIndex(l => new { l.ClientId, l.CreatedAt })
            .HasDatabaseName("IX_Leads_ClientId_CreatedAt")
            .HasFilter("IsActive = 1");
    }

    /// <summary>
    /// Índices recomendados para Clients
    /// </summary>
    private static void ApplyClientIndexes(ModelBuilder modelBuilder)
    {
        // Índice compuesto para paginación por fecha de creación
        modelBuilder
            .Entity<Model.Client>()
            .HasIndex(c => new { c.CreatedAt, c.Id })
            .HasDatabaseName("IX_Clients_CreatedAt_Id")
            .HasFilter("IsActive = 1");

        // Índice para búsquedas por nombre
        modelBuilder
            .Entity<Model.Client>()
            .HasIndex(c => new { c.Name, c.CreatedAt })
            .HasDatabaseName("IX_Clients_Name_CreatedAt")
            .HasFilter("IsActive = 1");

        // Índice para búsquedas por email
        modelBuilder
            .Entity<Model.Client>()
            .HasIndex(c => new { c.Email, c.CreatedAt })
            .HasDatabaseName("IX_Clients_Email_CreatedAt")
            .HasFilter("IsActive = 1");

        // Índice para filtros por tipo
        modelBuilder
            .Entity<Model.Client>()
            .HasIndex(c => new { c.Type, c.CreatedAt })
            .HasDatabaseName("IX_Clients_Type_CreatedAt")
            .HasFilter("IsActive = 1");
    }

    /// <summary>
    /// Índices recomendados para Reservations
    /// </summary>
    private static void ApplyReservationIndexes(ModelBuilder modelBuilder)
    {
        // Índice compuesto para paginación por fecha de creación
        modelBuilder
            .Entity<Reservation>()
            .HasIndex(r => new { r.CreatedAt, r.Id })
            .HasDatabaseName("IX_Reservations_CreatedAt_Id")
            .HasFilter("IsActive = 1");

        // Índice para filtros por estado
        modelBuilder
            .Entity<Reservation>()
            .HasIndex(r => new { r.Status, r.CreatedAt })
            .HasDatabaseName("IX_Reservations_Status_CreatedAt")
            .HasFilter("IsActive = 1");

        // Índice para filtros por cliente
        modelBuilder
            .Entity<Reservation>()
            .HasIndex(r => new { r.ClientId, r.CreatedAt })
            .HasDatabaseName("IX_Reservations_ClientId_CreatedAt")
            .HasFilter("IsActive = 1");

        // Índice para filtros por fecha de reserva
        modelBuilder
            .Entity<Reservation>()
            .HasIndex(r => new { r.ReservationDate, r.CreatedAt })
            .HasDatabaseName("IX_Reservations_ReservationDate_CreatedAt")
            .HasFilter("IsActive = 1");

        // Índice para filtros por cotización
        modelBuilder
            .Entity<Reservation>()
            .HasIndex(r => new { r.QuotationId, r.CreatedAt })
            .HasDatabaseName("IX_Reservations_QuotationId_CreatedAt")
            .HasFilter("IsActive = 1");
    }

    /// <summary>
    /// Índices recomendados para Quotations
    /// </summary>
    private static void ApplyQuotationIndexes(ModelBuilder modelBuilder)
    {
        // Índice compuesto para paginación por fecha de creación
        modelBuilder
            .Entity<Quotation>()
            .HasIndex(q => new { q.CreatedAt, q.Id })
            .HasDatabaseName("IX_Quotations_CreatedAt_Id")
            .HasFilter("IsActive = 1");

        // Índice para filtros por estado
        modelBuilder
            .Entity<Quotation>()
            .HasIndex(q => new { q.Status, q.CreatedAt })
            .HasDatabaseName("IX_Quotations_Status_CreatedAt")
            .HasFilter("IsActive = 1");

        // Índice para filtros por lead
        modelBuilder
            .Entity<Quotation>()
            .HasIndex(q => new { q.LeadId, q.CreatedAt })
            .HasDatabaseName("IX_Quotations_LeadId_CreatedAt")
            .HasFilter("IsActive = 1");

        // Índice para filtros por asesor
        modelBuilder
            .Entity<Quotation>()
            .HasIndex(q => new { q.AdvisorId, q.CreatedAt })
            .HasDatabaseName("IX_Quotations_AdvisorId_CreatedAt")
            .HasFilter("IsActive = 1");

        // Índice para filtros por lote
        modelBuilder
            .Entity<Quotation>()
            .HasIndex(q => new { q.LotId, q.CreatedAt })
            .HasDatabaseName("IX_Quotations_LotId_CreatedAt")
            .HasFilter("IsActive = 1");
    }

    /// <summary>
    /// Índices recomendados para Users
    /// </summary>
    private static void ApplyUserIndexes(ModelBuilder modelBuilder)
    {
        // Índice compuesto para paginación por fecha de creación
        modelBuilder
            .Entity<User>()
            .HasIndex(u => new { u.CreatedAt, u.Id })
            .HasDatabaseName("IX_Users_CreatedAt_Id")
            .HasFilter("IsActive = 1");

        // Índice para búsquedas por nombre
        modelBuilder
            .Entity<User>()
            .HasIndex(u => new { u.Name, u.CreatedAt })
            .HasDatabaseName("IX_Users_Name_CreatedAt")
            .HasFilter("IsActive = 1");

        // Índice para búsquedas por email
        modelBuilder
            .Entity<User>()
            .HasIndex(u => new { u.Email, u.CreatedAt })
            .HasDatabaseName("IX_Users_Email_CreatedAt")
            .HasFilter("IsActive = 1");

        // Índice para filtros por estado activo
        modelBuilder
            .Entity<User>()
            .HasIndex(u => new { u.IsActive, u.CreatedAt })
            .HasDatabaseName("IX_Users_IsActive_CreatedAt");
    }

    /// <summary>
    /// Índices recomendados para Projects
    /// </summary>
    private static void ApplyProjectIndexes(ModelBuilder modelBuilder)
    {
        // Índice compuesto para paginación por fecha de creación
        modelBuilder
            .Entity<Project>()
            .HasIndex(p => new { p.CreatedAt, p.Id })
            .HasDatabaseName("IX_Projects_CreatedAt_Id")
            .HasFilter("IsActive = 1");

        // Índice para búsquedas por nombre
        modelBuilder
            .Entity<Project>()
            .HasIndex(p => new { p.Name, p.CreatedAt })
            .HasDatabaseName("IX_Projects_Name_CreatedAt")
            .HasFilter("IsActive = 1");

        // Índice para filtros por estado activo
        modelBuilder
            .Entity<Project>()
            .HasIndex(p => new { p.IsActive, p.CreatedAt })
            .HasDatabaseName("IX_Projects_IsActive_CreatedAt");
    }

    /// <summary>
    /// Obtiene recomendaciones de índices para una entidad específica
    /// </summary>
    public static List<IndexRecommendation> GetRecommendationsForEntity<T>()
    {
        return typeof(T).Name switch
        {
            "Lead" => GetLeadRecommendations(),
            "Client" => GetClientRecommendations(),
            "Reservation" => GetReservationRecommendations(),
            "Quotation" => GetQuotationRecommendations(),
            "User" => GetUserRecommendations(),
            "Project" => GetProjectRecommendations(),
            _ => new List<IndexRecommendation>(),
        };
    }

    private static List<IndexRecommendation> GetLeadRecommendations()
    {
        return new List<IndexRecommendation>
        {
            new(
                "IX_Leads_CreatedAt_Id",
                "Paginación eficiente por fecha de creación",
                Priority.High
            ),
            new("IX_Leads_Status_CreatedAt", "Filtros por estado con paginación", Priority.High),
            new("IX_Leads_AssignedToId_CreatedAt", "Filtros por usuario asignado", Priority.Medium),
            new("IX_Leads_ProjectId_CreatedAt", "Filtros por proyecto", Priority.Medium),
            new("IX_Leads_ClientId_CreatedAt", "Filtros por cliente", Priority.Medium),
        };
    }

    private static List<IndexRecommendation> GetClientRecommendations()
    {
        return new List<IndexRecommendation>
        {
            new(
                "IX_Clients_CreatedAt_Id",
                "Paginación eficiente por fecha de creación",
                Priority.High
            ),
            new("IX_Clients_Name_CreatedAt", "Búsquedas por nombre con paginación", Priority.High),
            new("IX_Clients_Email_CreatedAt", "Búsquedas por email", Priority.Medium),
            new("IX_Clients_Type_CreatedAt", "Filtros por tipo de cliente", Priority.Low),
        };
    }

    private static List<IndexRecommendation> GetReservationRecommendations()
    {
        return new List<IndexRecommendation>
        {
            new(
                "IX_Reservations_CreatedAt_Id",
                "Paginación eficiente por fecha de creación",
                Priority.High
            ),
            new(
                "IX_Reservations_Status_CreatedAt",
                "Filtros por estado con paginación",
                Priority.High
            ),
            new("IX_Reservations_ClientId_CreatedAt", "Filtros por cliente", Priority.Medium),
            new(
                "IX_Reservations_ReservationDate_CreatedAt",
                "Filtros por fecha de reserva",
                Priority.Medium
            ),
            new("IX_Reservations_QuotationId_CreatedAt", "Filtros por cotización", Priority.Low),
        };
    }

    private static List<IndexRecommendation> GetQuotationRecommendations()
    {
        return new List<IndexRecommendation>
        {
            new(
                "IX_Quotations_CreatedAt_Id",
                "Paginación eficiente por fecha de creación",
                Priority.High
            ),
            new(
                "IX_Quotations_Status_CreatedAt",
                "Filtros por estado con paginación",
                Priority.High
            ),
            new("IX_Quotations_LeadId_CreatedAt", "Filtros por lead", Priority.Medium),
            new("IX_Quotations_AdvisorId_CreatedAt", "Filtros por asesor", Priority.Medium),
            new("IX_Quotations_LotId_CreatedAt", "Filtros por lote", Priority.Low),
        };
    }

    private static List<IndexRecommendation> GetUserRecommendations()
    {
        return new List<IndexRecommendation>
        {
            new(
                "IX_Users_CreatedAt_Id",
                "Paginación eficiente por fecha de creación",
                Priority.High
            ),
            new("IX_Users_Name_CreatedAt", "Búsquedas por nombre con paginación", Priority.High),
            new("IX_Users_Email_CreatedAt", "Búsquedas por email", Priority.Medium),
            new("IX_Users_IsActive_CreatedAt", "Filtros por estado activo", Priority.Medium),
        };
    }

    private static List<IndexRecommendation> GetProjectRecommendations()
    {
        return new List<IndexRecommendation>
        {
            new(
                "IX_Projects_CreatedAt_Id",
                "Paginación eficiente por fecha de creación",
                Priority.High
            ),
            new("IX_Projects_Name_CreatedAt", "Búsquedas por nombre con paginación", Priority.High),
            new("IX_Projects_IsActive_CreatedAt", "Filtros por estado activo", Priority.Medium),
        };
    }
}

/// <summary>
/// Recomendación de índice
/// </summary>
public class IndexRecommendation
{
    public string IndexName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Priority Priority { get; set; }
    public string[] Columns { get; set; } = Array.Empty<string>();
    public string? Filter { get; set; }

    public IndexRecommendation(string indexName, string description, Priority priority)
    {
        IndexName = indexName;
        Description = description;
        Priority = priority;
    }
}

/// <summary>
/// Prioridad del índice
/// </summary>
public enum Priority
{
    Low,
    Medium,
    High,
    Critical,
}
