using GestionHogar.Model;

namespace GestionHogar.Dtos;

public class ProjectDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Location { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public bool IsActive { get; set; }
    public decimal? DefaultDownPayment { get; set; }
    public int? DefaultFinancingMonths { get; set; }
    public decimal? MaxDiscountPercentage { get; set; } // Nuevo campo
    public string? ProjectUrlImage { get; set; } // URL de la imagen del proyecto
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }

    // Información adicional
    public int TotalBlocks { get; set; }
    public int TotalLots { get; set; }
    public int AvailableLots { get; set; }
    public int QuotedLots { get; set; }
    public int ReservedLots { get; set; }
    public int SoldLots { get; set; }

    public static ProjectDTO FromEntity(Project project)
    {
        return new ProjectDTO
        {
            Id = project.Id,
            Name = project.Name,
            Location = project.Location,
            Currency = project.Currency,
            IsActive = project.IsActive,
            DefaultDownPayment = project.DefaultDownPayment,
            DefaultFinancingMonths = project.DefaultFinancingMonths,
            MaxDiscountPercentage = project.MaxDiscountPercentage, // Nuevo campo
            ProjectUrlImage = project.ProjectUrlImage, // URL de la imagen
            CreatedAt = project.CreatedAt,
            ModifiedAt = project.ModifiedAt,
            TotalBlocks = project.Blocks?.Count ?? 0,
            TotalLots = project.Blocks?.SelectMany(b => b.Lots).Count() ?? 0,
            AvailableLots =
                project.Blocks?.SelectMany(b => b.Lots).Count(l => l.Status == LotStatus.Available)
                ?? 0,
            QuotedLots =
                project.Blocks?.SelectMany(b => b.Lots).Count(l => l.Status == LotStatus.Quoted)
                ?? 0,
            ReservedLots =
                project.Blocks?.SelectMany(b => b.Lots).Count(l => l.Status == LotStatus.Reserved)
                ?? 0,
            SoldLots =
                project.Blocks?.SelectMany(b => b.Lots).Count(l => l.Status == LotStatus.Sold) ?? 0,
        };
    }
}
