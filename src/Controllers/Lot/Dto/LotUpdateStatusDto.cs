using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;

namespace GestionHogar.Dtos;

public class LotStatusUpdateDTO
{
    [Required]
    public required LotStatus Status { get; set; }
}
