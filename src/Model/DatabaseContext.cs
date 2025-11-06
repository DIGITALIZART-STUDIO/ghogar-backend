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

    public DbSet<PaymentTransactionPayment> PaymentTransactionPayments { get; set; } = null!;

    public DbSet<OtpCode> OtpCodes { get; set; } = null!;

    public DbSet<Referral> Referrals { get; set; } = null!;

    public DbSet<SupervisorSalesAdvisor> SupervisorSalesAdvisors { get; set; } = null!;

    public DbSet<Notification> Notifications { get; set; } = null!;

    public DbSet<ApiPeruConsultation> ApiPeruConsultations { get; set; } = null!;

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
                "PaymentTransactionPaymentLegacy", // Cambiar nombre para evitar conflicto
                l => l.HasOne(typeof(Payment)).WithMany().HasForeignKey("PaymentId"),
                r =>
                    r.HasOne(typeof(PaymentTransaction))
                        .WithMany()
                        .HasForeignKey("PaymentTransactionId")
            );

        // NUEVA: Configuración de la entidad PaymentTransactionPayment
        builder.Entity<PaymentTransactionPayment>(entity =>
        {
            entity.HasKey(ptp => new { ptp.PaymentTransactionId, ptp.PaymentId });

            entity
                .HasOne(ptp => ptp.PaymentTransaction)
                .WithMany(pt => pt.PaymentDetails)
                .HasForeignKey(ptp => ptp.PaymentTransactionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(ptp => ptp.Payment)
                .WithMany()
                .HasForeignKey(ptp => ptp.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

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

        // Configuración de SupervisorSalesAdvisor
        builder.Entity<SupervisorSalesAdvisor>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Relación con Supervisor (User)
            entity
                .HasOne(e => e.Supervisor)
                .WithMany()
                .HasForeignKey(e => e.SupervisorId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relación con SalesAdvisor (User)
            entity
                .HasOne(e => e.SalesAdvisor)
                .WithMany()
                .HasForeignKey(e => e.SalesAdvisorId)
                .OnDelete(DeleteBehavior.Cascade);

            // Índices para optimizar consultas
            entity.HasIndex(e => e.SupervisorId);
            entity.HasIndex(e => e.SalesAdvisorId);
            entity.HasIndex(e => new { e.SupervisorId, e.SalesAdvisorId }).IsUnique();
        });

        // Configuración de ApiPeruConsultation
        builder.Entity<ApiPeruConsultation>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Configurar propiedades
            entity.Property(e => e.DocumentNumber).IsRequired().HasMaxLength(20);
            entity.Property(e => e.DocumentType).IsRequired().HasMaxLength(10);
            entity.Property(e => e.ResponseData).IsRequired();
            entity.Property(e => e.CompanyName).HasMaxLength(500);
            entity.Property(e => e.PersonName).HasMaxLength(500);
            entity.Property(e => e.Address).HasMaxLength(1000);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.Condition).HasMaxLength(50);

            // Índices para optimizar consultas
            entity.HasIndex(e => new { e.DocumentNumber, e.DocumentType });
            entity.HasIndex(e => e.ConsultedAt);
            entity.HasIndex(e => e.CompanyName);
            entity.HasIndex(e => e.PersonName);
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
