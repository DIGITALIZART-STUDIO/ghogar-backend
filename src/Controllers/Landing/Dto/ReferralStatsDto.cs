namespace GestionHogar.Controllers.Dtos;

public class ReferralStatsDto
{
    public int TotalReferrals { get; set; }
    public int ConvertedReferrals { get; set; }
    public int PendingReferrals { get; set; }
    public int InFollowUpReferrals { get; set; }
    public decimal ConversionRate { get; set; }
}
