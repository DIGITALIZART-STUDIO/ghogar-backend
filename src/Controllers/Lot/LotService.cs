using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GestionHogar.Dtos;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Services;

public class LotService : ILotService
{
    private readonly DatabaseContext _context;

    public LotService(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<LotDTO>> GetAllLotsAsync()
    {
        var lots = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .OrderBy(l => l.Block.Project.Name)
            .ThenBy(l => l.Block.Name)
            .ThenBy(l => l.LotNumber)
            .ToListAsync();

        return lots.Select(LotDTO.FromEntity);
    }

    public async Task<IEnumerable<LotDTO>> GetLotsByBlockIdAsync(Guid blockId)
    {
        var lots = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .Where(l => l.BlockId == blockId)
            .OrderBy(l => l.LotNumber)
            .ToListAsync();

        return lots.Select(LotDTO.FromEntity);
    }

    public async Task<IEnumerable<LotDTO>> GetLotsByProjectIdAsync(Guid projectId)
    {
        var lots = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .Where(l => l.Block.ProjectId == projectId)
            .OrderBy(l => l.Block.Name)
            .ThenBy(l => l.LotNumber)
            .ToListAsync();

        return lots.Select(LotDTO.FromEntity);
    }

    public async Task<IEnumerable<LotDTO>> GetLotsByStatusAsync(LotStatus status)
    {
        var lots = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .Where(l => l.Status == status)
            .OrderBy(l => l.Block.Project.Name)
            .ThenBy(l => l.Block.Name)
            .ThenBy(l => l.LotNumber)
            .ToListAsync();

        return lots.Select(LotDTO.FromEntity);
    }

    public async Task<IEnumerable<LotDTO>> GetAvailableLotsAsync()
    {
        return await GetLotsByStatusAsync(LotStatus.Available);
    }

    public async Task<LotDTO?> GetLotByIdAsync(Guid id)
    {
        var lot = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .FirstOrDefaultAsync(l => l.Id == id);

        return lot != null ? LotDTO.FromEntity(lot) : null;
    }

    public async Task<LotDTO> CreateLotAsync(LotCreateDTO dto)
    {
        // Verificar que el bloque existe y está activo
        var block = await _context
            .Blocks.Include(b => b.Project)
            .FirstOrDefaultAsync(b => b.Id == dto.BlockId);

        if (block == null)
            throw new InvalidOperationException($"Bloque con ID {dto.BlockId} no encontrado");

        if (!block.IsActive)
            throw new InvalidOperationException("No se puede crear un lote en un bloque inactivo");

        if (!block.Project.IsActive)
            throw new InvalidOperationException(
                "No se puede crear un lote en un proyecto inactivo"
            );

        // Verificar que no existe un lote con el mismo número en el bloque
        var existingLot = await _context.Lots.FirstOrDefaultAsync(l =>
            l.BlockId == dto.BlockId && l.LotNumber.ToLower() == dto.LotNumber.ToLower()
        );

        if (existingLot != null)
            throw new InvalidOperationException(
                $"Ya existe un lote con el número '{dto.LotNumber}' en este bloque"
            );

        var lot = dto.ToEntity();
        _context.Lots.Add(lot);
        await _context.SaveChangesAsync();

        // Recargar el lote con sus relaciones
        var createdLot = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .FirstAsync(l => l.Id == lot.Id);

        return LotDTO.FromEntity(createdLot);
    }

    public async Task<LotDTO?> UpdateLotAsync(Guid id, LotUpdateDTO dto)
    {
        var lot = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lot == null)
            return null;

        // Si se está cambiando el número, verificar que no exista otro lote con ese número en el bloque
        if (
            !string.IsNullOrWhiteSpace(dto.LotNumber)
            && dto.LotNumber.ToLower() != lot.LotNumber.ToLower()
        )
        {
            var existingLot = await _context.Lots.FirstOrDefaultAsync(l =>
                l.BlockId == lot.BlockId
                && l.LotNumber.ToLower() == dto.LotNumber.ToLower()
                && l.Id != id
            );

            if (existingLot != null)
                throw new InvalidOperationException(
                    $"Ya existe un lote con el número '{dto.LotNumber}' en este bloque"
                );
        }

        // Validar cambio de estado si se especifica
        if (dto.Status.HasValue && dto.Status.Value != lot.Status)
        {
            if (!await CanChangeLotStatusAsync(id, dto.Status.Value))
                throw new InvalidOperationException(
                    $"No se puede cambiar el estado del lote de {lot.Status} a {dto.Status.Value}"
                );
        }

        dto.ApplyTo(lot);
        await _context.SaveChangesAsync();

        // Recargar el lote con sus relaciones
        var updatedLot = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .FirstAsync(l => l.Id == id);

        return LotDTO.FromEntity(updatedLot);
    }

    public async Task<LotDTO?> UpdateLotStatusAsync(Guid id, LotStatus status)
    {
        var lot = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lot == null)
            return null;

        if (!await CanChangeLotStatusAsync(id, status))
            throw new InvalidOperationException(
                $"No se puede cambiar el estado del lote de {lot.Status} a {status}"
            );

        lot.Status = status;
        lot.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return LotDTO.FromEntity(lot);
    }

    public async Task<bool> DeleteLotAsync(Guid id)
    {
        var lot = await _context.Lots.FindAsync(id);
        if (lot == null)
            return false;

        // No se puede eliminar un lote reservado o vendido
        if (lot.Status == LotStatus.Reserved || lot.Status == LotStatus.Sold)
            throw new InvalidOperationException("No se puede eliminar un lote reservado o vendido");

        // TODO: Verificar que no tenga cotizaciones activas
        // var hasActiveQuotations = await _context.Quotations
        //     .AnyAsync(q => q.LotId == id && q.Status == QuotationStatus.Active);
        // if (hasActiveQuotations)
        //     throw new InvalidOperationException("No se puede eliminar un lote con cotizaciones activas");

        _context.Lots.Remove(lot);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ActivateLotAsync(Guid id)
    {
        var lot = await _context.Lots.FindAsync(id);
        if (lot == null)
            return false;

        lot.IsActive = true;
        lot.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeactivateLotAsync(Guid id)
    {
        var lot = await _context.Lots.FindAsync(id);
        if (lot == null)
            return false;

        // No se puede desactivar un lote reservado o vendido
        if (lot.Status == LotStatus.Reserved || lot.Status == LotStatus.Sold)
            throw new InvalidOperationException(
                "No se puede desactivar un lote reservado o vendido"
            );

        lot.IsActive = false;
        lot.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> LotExistsAsync(Guid id)
    {
        return await _context.Lots.AnyAsync(l => l.Id == id);
    }

    public async Task<bool> LotExistsInBlockAsync(Guid blockId, string lotNumber)
    {
        return await _context.Lots.AnyAsync(l =>
            l.BlockId == blockId && l.LotNumber.ToLower() == lotNumber.ToLower()
        );
    }

    public async Task<bool> CanChangeLotStatusAsync(Guid id, LotStatus newStatus)
    {
        var lot = await _context.Lots.FindAsync(id);
        if (lot == null)
            return false;

        var currentStatus = lot.Status;

        // Reglas de negocio para cambio de estado
        return (currentStatus, newStatus) switch
        {
            // Desde Available se puede ir a cualquier estado
            (LotStatus.Available, _) => true,

            // Desde Quoted se puede volver a Available o avanzar a Reserved
            (LotStatus.Quoted, LotStatus.Available) => true,
            (LotStatus.Quoted, LotStatus.Reserved) => true,

            // Desde Reserved se puede volver a Available o avanzar a Sold
            (LotStatus.Reserved, LotStatus.Available) => true,
            (LotStatus.Reserved, LotStatus.Sold) => true,

            // Desde Sold no se puede cambiar a ningún estado
            (LotStatus.Sold, _) => false,

            // Cualquier otro cambio no permitido
            _ => false,
        };
    }
}
