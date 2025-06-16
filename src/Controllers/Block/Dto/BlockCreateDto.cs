using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;

namespace GestionHogar.Dtos;

public class BlockCreateDTO
{
    [Required]
    [StringLength(50)]
    public required string Name { get; set; }

    [Required]
    public required Guid ProjectId { get; set; }

    public Block ToEntity()
    {
        return new Block
        {
            Name = Name.Trim(),
            ProjectId = ProjectId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
        };
    }
}
