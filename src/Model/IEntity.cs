using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Model;

/// <summary>
/// Represents a base entity
/// </summary>
public interface IEntity
{
    Guid Id { get; set; }
    bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}

/// <summary>
/// Represents a base model. This model contains a SetUp static method,
/// which handles default values for CreatedAt and ModifiedAt.
/// </summary>
public abstract class BaseModel : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public static void SetUp<A>(ModelBuilder modelBuilder)
        where A : class, IEntity
    {
        modelBuilder.Entity<A>().Property(b => b.CreatedAt).HasDefaultValueSql("NOW()");
        modelBuilder.Entity<A>().Property(b => b.ModifiedAt).HasDefaultValueSql("NOW()");
    }
}

/// <summary>
/// This interface represents a Type that can be mapped to another.
/// For example, there may be a Client entity, and a ClientCreateDTO.
/// This interface would map ClientCreateDTO to Client, like:
/// `class ClientCreateDTO : IMapToEntity<Client>`
/// </summary>
public interface IMapToEntity<T>
    where T : class, IEntity
{
    T MapToEntity();
}

/// <summary>
/// This interface represents a Type that can modify an entity.
/// For example, there may be a Client entity, and a ClientPatchDTO.
/// This interface would take in a Client and mutate its state, like:
/// `class ClientPatchDTO : IEntityPatcher<Client>`
/// </summary>
public interface IEntityPatcher<T>
    where T : class, IEntity
{
    void ApplyPatch(T entity);
}
