using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;

namespace GestionHogar.Dtos;

public class ProjectCreateDTO
{
    [Required]
    [StringLength(100)]
    public required string Name { get; set; }

    [Required]
    [StringLength(200)]
    public required string Location { get; set; }

    [Required]
    [StringLength(3)]
    public required string Currency { get; set; } // USD, PEN, MXN, etc.

    [Range(0, 100)]
    public decimal? DefaultDownPayment { get; set; } // Porcentaje (ej: 10.0 para 10%)

    [Range(1, 360)]
    public int? DefaultFinancingMonths { get; set; }

    public Project ToEntity()
    {
        return new Project
        {
            Name = Name,
            Location = Location,
            Currency = Currency.ToUpper(),
            IsActive = true,
            DefaultDownPayment = DefaultDownPayment,
            DefaultFinancingMonths = DefaultFinancingMonths,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
        };
    }
}
