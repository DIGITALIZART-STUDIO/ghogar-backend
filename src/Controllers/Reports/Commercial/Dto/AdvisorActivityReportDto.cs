using System.Text.Json.Serialization;
using GestionHogar.Model;

namespace GestionHogar.Controllers.Reports.Dto;

/// <summary>
/// Fila principal del reporte comercial de actividad de asesores.
/// Representa un Lead con su información de asesor, estado y actividad agregada de tareas.
/// </summary>
public class AdvisorActivityReportItemDto
{
    public Guid LeadId { get; set; }
    public string LeadCode { get; set; } = string.Empty;

    public Guid? ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string ClientPhone { get; set; } = string.Empty;

    public Guid? AssignedToId { get; set; }

    /// <summary>
    /// Null si el lead no tiene asesor asignado (se muestra como "Sin asignar" en el frontend)
    /// </summary>
    public string? AssignedToName { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LeadStatus Status { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LeadCaptureSource CaptureSource { get; set; }

    public DateTime EntryDate { get; set; }

    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }

    /// <summary>
    /// Cantidad total de tareas activas asociadas al lead
    /// </summary>
    public int TaskCount { get; set; }

    /// <summary>
    /// Fecha de la última tarea (CompletedDate si existe, si no ScheduledDate). Null si no tiene tareas.
    /// </summary>
    public DateTime? LastTaskDate { get; set; }
}

/// <summary>
/// Detalle de una tarea de un lead, usado en la fila expandible de la tabla
/// y en la exportación a Excel.
/// </summary>
public class AdvisorActivityTaskDto
{
    public Guid Id { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TaskType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public bool IsCompleted { get; set; }
}
