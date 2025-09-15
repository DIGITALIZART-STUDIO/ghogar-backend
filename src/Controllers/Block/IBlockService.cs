using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GestionHogar.Dtos;
using GestionHogar.Model;

namespace GestionHogar.Services;

public interface IBlockService
{
    Task<IEnumerable<BlockDTO>> GetAllBlocksAsync();
    Task<IEnumerable<BlockDTO>> GetBlocksByProjectIdAsync(Guid projectId);
    Task<IEnumerable<BlockDTO>> GetActiveBlocksByProjectIdAsync(Guid projectId);
    Task<PaginatedResponseV2<BlockDTO>> GetActiveBlocksByProjectIdPaginatedAsync(
        Guid projectId,
        int page,
        int pageSize,
        string? search = null,
        string? orderBy = null,
        string? orderDirection = "asc",
        string? preselectedId = null
    );
    Task<BlockDTO?> GetBlockByIdAsync(Guid id);
    Task<BlockDTO> CreateBlockAsync(BlockCreateDTO dto);
    Task<BlockDTO?> UpdateBlockAsync(Guid id, BlockUpdateDTO dto);
    Task<bool> DeleteBlockAsync(Guid id);
    Task<bool> ActivateBlockAsync(Guid id);
    Task<bool> DeactivateBlockAsync(Guid id);
    Task<bool> BlockExistsAsync(Guid id);
    Task<bool> BlockExistsInProjectAsync(Guid projectId, string name);
}
