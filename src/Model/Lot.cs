using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionHogar.Model;

public class Lot : IEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public required string LotNumber { get; set; } // e.g. "12", "A-5"

    [Required]
    public decimal Area { get; set; } // in square meters

    [Required]
    public decimal Price { get; set; }

    [Required]
    public LotStatus Status { get; set; } = LotStatus.Available;

    [Required]
    public Guid BlockId { get; set; }

    [ForeignKey("BlockId")]
    public Block Block { get; set; } = default!;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}

public enum LotStatus
{
    Available,
    Quoted,
    Reserved,
    Sold,
}
