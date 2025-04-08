using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Model;

public enum AuditAction
{
    Create,
    Update,
    Delete,
}

public class Audit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string EntityId { get; set; }
    public required string EntityType { get; set; }
    public required AuditAction Action { get; set; }
    public Guid? UserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public static void SetUp(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Audit>().Property(b => b.CreatedAt).HasDefaultValueSql("NOW()");
    }
}
