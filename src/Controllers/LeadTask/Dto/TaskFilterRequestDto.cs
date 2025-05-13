using System;
using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;

namespace GestionHogar.Controllers.Dtos;

public class TaskFilterRequest
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public Guid? AssignedToId { get; set; }
    public Guid? LeadId { get; set; }
    public string? Type { get; set; }
    public bool? IsCompleted { get; set; }
}
