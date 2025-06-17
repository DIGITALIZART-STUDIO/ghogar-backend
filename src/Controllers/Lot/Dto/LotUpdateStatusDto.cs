using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using GestionHogar.Model;

namespace GestionHogar.Dtos;

public class LotStatusUpdateDTO
{
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required LotStatus Status { get; set; }
}
