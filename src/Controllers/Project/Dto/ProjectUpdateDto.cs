using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;

namespace GestionHogar.Dtos;

public class ProjectUpdateDTO
{
    [StringLength(100)]
    public string? Name { get; set; }

    [StringLength(200)]
    public string? Location { get; set; }

    [StringLength(3)]
    public string? Currency { get; set; }

    public bool? IsActive { get; set; }

    [Range(0, 100)]
    public decimal? DefaultDownPayment { get; set; }

    [Range(1, 360)]
    public int? DefaultFinancingMonths { get; set; }

    [Range(0, 100)]
    public decimal? MaxDiscountPercentage { get; set; } // Nuevo campo

    public void ApplyTo(Project project)
    {
        if (!string.IsNullOrWhiteSpace(Name))
            project.Name = Name;

        if (!string.IsNullOrWhiteSpace(Location))
            project.Location = Location;

        if (!string.IsNullOrWhiteSpace(Currency))
            project.Currency = Currency.ToUpper();

        if (IsActive.HasValue)
            project.IsActive = IsActive.Value;

        if (DefaultDownPayment.HasValue)
            project.DefaultDownPayment = DefaultDownPayment.Value;

        if (DefaultFinancingMonths.HasValue)
            project.DefaultFinancingMonths = DefaultFinancingMonths.Value;

        if (MaxDiscountPercentage.HasValue)
            project.MaxDiscountPercentage = MaxDiscountPercentage.Value; // Nuevo campo

        project.ModifiedAt = DateTime.UtcNow;
    }
}
