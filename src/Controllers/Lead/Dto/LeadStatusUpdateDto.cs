using GestionHogar.Model;

namespace GestionHogar.Controllers.Dtos;

public class LeadStatusUpdateDto
{
    public LeadStatus Status { get; set; }
    public LeadCompletionReason? CompletionReason { get; set; }
}
