using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;

namespace GestionHogar.Dtos;

public class BlockUpdateDTO
{
    [StringLength(50)]
    public string? Name { get; set; }

    public bool? IsActive { get; set; }

    public void ApplyTo(Block block)
    {
        if (!string.IsNullOrWhiteSpace(Name))
            block.Name = Name.Trim();

        if (IsActive.HasValue)
            block.IsActive = IsActive.Value;

        block.ModifiedAt = DateTime.UtcNow;
    }
}
