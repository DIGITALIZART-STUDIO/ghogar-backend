using Microsoft.AspNetCore.Identity;

namespace GestionHogar.Model;

public class SupervisorSalesAdvisor : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool IsActive { get; set; } = true;
    public Guid SupervisorId { get; set; }
    public Guid SalesAdvisorId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User Supervisor { get; set; } = null!;
    public User SalesAdvisor { get; set; } = null!;
}
