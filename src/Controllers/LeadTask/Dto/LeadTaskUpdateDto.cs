using System;
using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;

namespace GestionHogar.Controllers.Dtos;

public class LeadTaskUpdateDto
{
    public Guid? LeadId { get; set; }
    public Guid? AssignedToId { get; set; }
    public string? Description { get; set; }
    public string? ScheduledDate { get; set; } // Formato "yyyy-MM-dd"
    public string? CompletedDate { get; set; } // Formato "2025-04-22T06:34:00Z"
    public TaskType? Type { get; set; }
    public bool? IsCompleted { get; set; }
}
