using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Model;

public class DatabaseContext(DbContextOptions<DatabaseContext> options)
    : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options)
{
    private readonly Guid? _currentUserId = null;

    public DatabaseContext(
        DbContextOptions<DatabaseContext> options,
        IHttpContextAccessor httpContextAccessor
    )
        : this(options)
    {
        var currentUserId = httpContextAccessor
            .HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)
            ?.Value;
        _currentUserId = currentUserId != null ? Guid.Parse(currentUserId) : null;
    }

    public required DbSet<Audit> Audits { get; set; }
    public DbSet<Client> Clients { get; set; } = null!;

    public DbSet<Lead> Leads { get; set; }

    public DbSet<LeadTask> LeadTasks { get; set; }

    public DbSet<Quotation> Quotations { get; set; }

    public DbSet<Project> Projects { get; set; } = null!;
    public DbSet<Block> Blocks { get; set; } = null!;

    public DbSet<Lot> Lots { get; set; } = null!;

    public DbSet<Reservation> Reservations { get; set; } = null!;

    public DbSet<Payment> Payments { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        Audit.SetUp(builder);
        BaseModel.SetUp<Reservation>(builder);
        BaseModel.SetUp<Payment>(builder);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        // Create audits
        var audits = AuditActions();
        if (audits.Count != 0)
        {
            Audits.AddRange(audits);
        }
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        // Create audits
        var audits = AuditActions();
        if (audits.Count != 0)
        {
            Audits.AddRange(audits);
        }
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker
            .Entries()
            .Where(e => e.Entity is IEntity && e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            var entity = (IEntity)entry.Entity;
            var now = DateTime.UtcNow;

            entity.ModifiedAt = now;
        }
    }

    private List<Audit> AuditActions()
    {
        var audits = new List<Audit>();
        var entries = ChangeTracker.Entries().Where(e => e.Entity is IEntity);

        foreach (var entry in entries)
        {
            Audit? auditEntry = null;
            var entity = (IEntity)entry.Entity;

            switch (entry.State)
            {
                case EntityState.Added:
                    auditEntry = new Audit
                    {
                        EntityId = entity.Id.ToString(),
                        EntityType = entity.GetType().Name,
                        Action = AuditAction.Create,
                        UserId = _currentUserId,
                    };
                    break;
                case EntityState.Modified:
                    auditEntry = new Audit
                    {
                        EntityId = entity.Id.ToString(),
                        EntityType = entity.GetType().Name,
                        Action = AuditAction.Update,
                        UserId = _currentUserId,
                    };
                    break;
                case EntityState.Deleted:
                    auditEntry = new Audit
                    {
                        EntityId = entity.Id.ToString(),
                        EntityType = entity.GetType().Name,
                        Action = AuditAction.Delete,
                        UserId = _currentUserId,
                    };
                    break;
            }

            if (auditEntry != null)
            {
                audits.Add(auditEntry);
            }
        }

        return audits;
    }
}
