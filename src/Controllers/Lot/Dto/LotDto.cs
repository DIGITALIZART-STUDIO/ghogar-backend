using GestionHogar.Model;

namespace GestionHogar.Dtos;

public class LotDTO
{
    public Guid Id { get; set; }
    public string LotNumber { get; set; } = null!;
    public decimal Area { get; set; }
    public decimal Price { get; set; }
    public LotStatus Status { get; set; }
    public string StatusText { get; set; } = null!;
    public Guid BlockId { get; set; }
    public string BlockName { get; set; } = null!;
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }

    // Calculados
    public decimal PricePerSquareMeter { get; set; }

    public static LotDTO FromEntity(Lot lot)
    {
        return new LotDTO
        {
            Id = lot.Id,
            LotNumber = lot.LotNumber,
            Area = lot.Area,
            Price = lot.Price,
            Status = lot.Status,
            StatusText = GetStatusText(lot.Status),
            BlockId = lot.BlockId,
            BlockName = lot.Block?.Name ?? "",
            ProjectId = lot.Block?.ProjectId ?? Guid.Empty,
            ProjectName = lot.Block?.Project?.Name ?? "",
            IsActive = lot.IsActive,
            CreatedAt = lot.CreatedAt,
            ModifiedAt = lot.ModifiedAt,
            PricePerSquareMeter = lot.Area > 0 ? lot.Price / lot.Area : 0,
        };
    }

    private static string GetStatusText(LotStatus status)
    {
        return status switch
        {
            LotStatus.Available => "Disponible",
            LotStatus.Quoted => "Cotizado",
            LotStatus.Reserved => "Reservado",
            LotStatus.Sold => "Vendido",
            _ => "Desconocido",
        };
    }
}
