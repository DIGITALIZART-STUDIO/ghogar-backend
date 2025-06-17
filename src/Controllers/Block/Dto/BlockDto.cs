using GestionHogar.Model;

namespace GestionHogar.Dtos;

public class BlockDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }

    // Información adicional
    public int TotalLots { get; set; }
    public int AvailableLots { get; set; }
    public int QuotedLots { get; set; }
    public int ReservedLots { get; set; }
    public int SoldLots { get; set; }

    public static BlockDTO FromEntity(Block block)
    {
        return new BlockDTO
        {
            Id = block.Id,
            Name = block.Name,
            ProjectId = block.ProjectId,
            ProjectName = block.Project?.Name ?? "",
            IsActive = block.IsActive,
            CreatedAt = block.CreatedAt,
            ModifiedAt = block.ModifiedAt,
            TotalLots = block.Lots?.Count ?? 0,
            AvailableLots = block.Lots?.Count(l => l.Status == LotStatus.Available) ?? 0,
            QuotedLots = block.Lots?.Count(l => l.Status == LotStatus.Quoted) ?? 0,
            ReservedLots = block.Lots?.Count(l => l.Status == LotStatus.Reserved) ?? 0,
            SoldLots = block.Lots?.Count(l => l.Status == LotStatus.Sold) ?? 0,
        };
    }
}
