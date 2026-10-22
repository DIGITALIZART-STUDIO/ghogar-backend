using System.ComponentModel.DataAnnotations;

namespace GestionHogar.Dtos;

public class ProjectContractTemplateDto
{
    public bool Exists { get; set; }
    public Guid? Id { get; set; }
    public Guid ProjectId { get; set; }
    public string? HtmlContent { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

public class ProjectContractSaveDto
{
    [Required]
    public required string HtmlContent { get; set; }
}

public class ContractVariableDto
{
    public required string Key { get; set; }
    public required string Label { get; set; }
    public required string Group { get; set; }
}
