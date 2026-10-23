using GestionHogar.Dtos;
using Microsoft.AspNetCore.Http;

namespace GestionHogar.Services;

public interface IProjectContractService
{
    IReadOnlyList<ContractVariableDto> GetContractVariables();
    Task<ProjectContractTemplateDto> GetByProjectIdAsync(Guid projectId);
    Task<ProjectContractTemplateDto> SaveAsync(Guid projectId, string htmlContent, Guid userId);
    Task<ProjectContractTemplateDto> ImportFromDocxAsync(
        Guid projectId,
        IFormFile file,
        Guid userId
    );
    Task<bool> DeleteAsync(Guid projectId);
    Task<byte[]> RenderFilledDocxAsync(Guid projectId, Dictionary<string, string> placeholders);
}
