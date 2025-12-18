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

    public DbSet<PaymentTransaction> PaymentTransactions { get; set; } = null!;

    public DbSet<OtpCode> OtpCodes { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        Audit.SetUp(builder);
        BaseModel.SetUp<Reservation>(builder);
        BaseModel.SetUp<Payment>(builder);
        BaseModel.SetUp<PaymentTransaction>(builder);
        PaymentTransaction.SetUp<PaymentTransaction>(builder);

        // Configuración de la relación PaymentTransaction - Payment
        builder
            .Entity<PaymentTransaction>()
            .HasMany(pt => pt.Payments)
            .WithMany()
            .UsingEntity(
                "PaymentTransactionPayments",
                l => l.HasOne(typeof(Payment)).WithMany().HasForeignKey("PaymentId"),
                r =>
                    r.HasOne(typeof(PaymentTransaction))
                        .WithMany()
                        .HasForeignKey("PaymentTransactionId")
            );

        // Configuración de OtpCode
        builder.Entity<OtpCode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(6);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();

            // Relación con User
            entity
                .HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Índices para optimizar consultas
            entity.HasIndex(e => new
            {
                e.UserId,
                e.Code,
                e.IsActive,
            });
            entity.HasIndex(e => e.ExpiresAt);
        });
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
