using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionHogar.Model;

public class Referral : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Cliente que hace la referencia (referidor)
    [Required]
    public Guid ReferrerClientId { get; set; }

    [ForeignKey("ReferrerClientId")]
    public Client ReferrerClient { get; set; } = null!;

    // Lead del referenciado (la persona que fue referida)
    [Required]
    public Guid ReferredLeadId { get; set; }

    [ForeignKey("ReferredLeadId")]
    public Lead ReferredLead { get; set; } = null!;

    // Proyecto de interés del referido (opcional, puede venir del formulario)
    public Guid? ProjectId { get; set; }

    [ForeignKey("ProjectId")]
    public Project? Project { get; set; }

    // Propiedades de IEntity
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}
