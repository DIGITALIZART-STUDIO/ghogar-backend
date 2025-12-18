using GestionHogar.Model;

namespace GestionHogar.Controllers.Dtos;

public class ReferralResultDto
{
    public Guid ReferralId { get; set; }
    public Guid ReferrerClientId { get; set; }
    public Guid ReferredLeadId { get; set; }
    public string ReferredLeadCode { get; set; } = string.Empty;

    // Información sobre qué se creó o encontró
    public ReferralProcessInfo ProcessInfo { get; set; } = new();

    // Mensaje de éxito
    public string Message { get; set; } = string.Empty;
}

public class ReferralProcessInfo
{
    // Referidor
    public bool ReferrerClientExisted { get; set; }
    public bool ReferrerClientCreated { get; set; }

    // Referenciado
    public bool ReferredClientExisted { get; set; }
    public bool ReferredClientCreated { get; set; }
    public bool ReferredLeadCreated { get; set; }

    // Referral
    public bool ReferralCreated { get; set; }
}
