using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using GestionHogar.Model;

namespace GestionHogar.Dtos;

public class LotUpdateDTO
{
    [StringLength(50)]
    public string? LotNumber { get; set; }

    [Range(1, double.MaxValue, ErrorMessage = "El área debe ser mayor a 0")]
    public decimal? Area { get; set; }

    [Range(1, double.MaxValue, ErrorMessage = "El precio debe ser mayor a 0")]
    public decimal? Price { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LotStatus? Status { get; set; }

    public bool? IsActive { get; set; }

    public void ApplyTo(Lot lot)
    {
        if (!string.IsNullOrWhiteSpace(LotNumber))
            lot.LotNumber = LotNumber.Trim();

        if (Area.HasValue)
            lot.Area = Area.Value;

        if (Price.HasValue)
            lot.Price = Price.Value;

        if (Status.HasValue)
            lot.Status = Status.Value;

        if (IsActive.HasValue)
            lot.IsActive = IsActive.Value;

        lot.ModifiedAt = DateTime.UtcNow;
    }
}

namespace GestionHogar.Dtos;

public class LotUpdateDTO
{
    [StringLength(50)]
    public string? LotNumber { get; set; }

    [Range(1, double.MaxValue, ErrorMessage = "El área debe ser mayor a 0")]
    public decimal? Area { get; set; }

    [Range(1, double.MaxValue, ErrorMessage = "El precio debe ser mayor a 0")]
    public decimal? Price { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LotStatus? Status { get; set; }

    public bool? IsActive { get; set; }

    public Guid? BlockId { get; set; }

    public void ApplyTo(Lot lot)
    {
        if (!string.IsNullOrWhiteSpace(LotNumber))
            lot.LotNumber = LotNumber.Trim();

        if (Area.HasValue)
            lot.Area = Area.Value;

        if (Price.HasValue)
            lot.Price = Price.Value;

        if (Status.HasValue)
            lot.Status = Status.Value;

        if (IsActive.HasValue)
            lot.IsActive = IsActive.Value;

        if (BlockId.HasValue)
            lot.BlockId = BlockId.Value;

        lot.ModifiedAt = DateTime.UtcNow;
    }
}
