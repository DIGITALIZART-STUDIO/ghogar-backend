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
    public TaskType? Type { get; set; }
    public bool? IsCompleted { get; set; }
}
