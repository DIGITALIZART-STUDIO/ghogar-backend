using System;
using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;

namespace GestionHogar.Controllers.Dtos;

public class LeadTaskCreateDto
{
    [Required]
    public Guid LeadId { get; set; }

    [Required]
    public Guid AssignedToId { get; set; }

    [Required]
    public required string Description { get; set; }

    [Required]
    public required string ScheduledDate { get; set; } // Formato "yyyy-MM-dd"

    public TaskType Type { get; set; } = TaskType.Other;
}
