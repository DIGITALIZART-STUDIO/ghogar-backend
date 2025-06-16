using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;

namespace GestionHogar.Dtos;

public class LotCreateDTO
{
    [Required]
    [StringLength(50)]
    public required string LotNumber { get; set; }

    [Required]
    [Range(1, double.MaxValue, ErrorMessage = "El área debe ser mayor a 0")]
    public required decimal Area { get; set; }

    [Required]
    [Range(1, double.MaxValue, ErrorMessage = "El precio debe ser mayor a 0")]
    public required decimal Price { get; set; }

    [Required]
    public required Guid BlockId { get; set; }

    public Lot ToEntity()
    {
        return new Lot
        {
            LotNumber = LotNumber.Trim(),
            Area = Area,
            Price = Price,
            BlockId = BlockId,
            Status = LotStatus.Available,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
        };
    }
}
