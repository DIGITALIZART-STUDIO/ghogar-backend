using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionHogar.Model;

public class Project : IEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public required string Name { get; set; }

    [Required]
    public required string Location { get; set; }

    [Required]
    [StringLength(3)]
    public required string Currency { get; set; } // e.g. "USD", "PEN", "MXN"

    [Required]
    public bool IsActive { get; set; } = true;

    public decimal? DefaultDownPayment { get; set; } // as percentage (e.g., 10.0)

    public int? DefaultFinancingMonths { get; set; }

    // Límite máximo de descuento permitido por proyecto (como porcentaje)
    [Range(0, 100)]
    public decimal? MaxDiscountPercentage { get; set; }

    // URL de la imagen del proyecto
    public string? ProjectUrlImage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Block> Blocks { get; set; } = new List<Block>();
}
