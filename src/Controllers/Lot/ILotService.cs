using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GestionHogar.Dtos;
using GestionHogar.Model;

namespace GestionHogar.Services;

public interface ILotService
{
    Task<IEnumerable<LotDTO>> GetAllLotsAsync();
    Task<IEnumerable<LotDTO>> GetLotsByBlockIdAsync(Guid blockId);
    Task<PaginatedResponseV2<LotDTO>> GetLotsByBlockIdPaginatedAsync(
        Guid blockId,
        int page,
        int pageSize,
        string? search = null,
        string? orderBy = null,
        string? orderDirection = "asc",
        string? preselectedId = null
    );
    Task<IEnumerable<LotDTO>> GetLotsByProjectIdAsync(Guid projectId);
    Task<IEnumerable<LotDTO>> GetLotsByStatusAsync(LotStatus status);
    Task<IEnumerable<LotDTO>> GetAvailableLotsAsync();
    Task<LotDTO?> GetLotByIdAsync(Guid id);
    Task<LotDTO> CreateLotAsync(LotCreateDTO dto);
    Task<LotDTO?> UpdateLotAsync(Guid id, LotUpdateDTO dto);
    Task<LotDTO?> UpdateLotStatusAsync(Guid id, LotStatus status);
    Task<bool> DeleteLotAsync(Guid id);
    Task<bool> ActivateLotAsync(Guid id);
    Task<bool> DeactivateLotAsync(Guid id);
    Task<bool> LotExistsAsync(Guid id);
    Task<bool> LotExistsInBlockAsync(Guid blockId, string lotNumber);
    Task<bool> CanChangeLotStatusAsync(Guid id, LotStatus newStatus);
}
