using System;
using System.Collections.Generic;

namespace GestionHogar.Dtos;

public class LeadTasksResponseDto
{
    public LeadDTO Lead { get; set; } = null!;
    public List<LeadTaskDTO> Tasks { get; set; } = new List<LeadTaskDTO>();
}

public class LeadDTO
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public ClientDTO Client { get; set; } = null!;
    public Guid AssignedToId { get; set; }
    public UserBasicDTO AssignedTo { get; set; } = null!;
    public string Status { get; set; } = null!;
    public bool IsActive { get; set; }
}

public class ClientDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Dni { get; set; }
    public string? Ruc { get; set; }
    public string? CompanyName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string Type { get; set; } = null!;
    public bool IsActive { get; set; }
}

public class UserBasicDTO
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; }
    // Información sensible omitida intencionalmente
}

public class LeadTaskDTO
{
    public Guid Id { get; set; }
    public Guid LeadId { get; set; }
    public LeadDTO? Lead { get; set; }
    public Guid AssignedToId { get; set; }
    public UserBasicDTO AssignedTo { get; set; } = null!;
    public string Description { get; set; } = null!;
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public bool IsCompleted { get; set; }
    public string Type { get; set; } = null!;
    public bool IsActive { get; set; }
}
