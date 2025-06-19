using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GestionHogar.Dtos;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Services;

public class QuotationService : IQuotationService
{
    private readonly DatabaseContext _context;

    public QuotationService(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<QuotationDTO>> GetAllQuotationsAsync()
    {
        var quotations = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l!.Client)
            .Include(q => q.Advisor)
            .Include(q => q.Lot) // **NUEVO: Incluir el lote**
            .ThenInclude(l => l!.Block)
            .ThenInclude(b => b.Project)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();

        return quotations.Select(QuotationDTO.FromEntity);
    }

    public async Task<QuotationDTO?> GetQuotationByIdAsync(Guid id)
    {
        var quotation = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l!.Client)
            .Include(q => q.Advisor)
            .Include(q => q.Lot) // **NUEVO: Incluir el lote**
            .ThenInclude(l => l!.Block)
            .ThenInclude(b => b.Project)
            .FirstOrDefaultAsync(q => q.Id == id);

        return quotation != null ? QuotationDTO.FromEntity(quotation) : null;
    }

    public async Task<IEnumerable<QuotationDTO>> GetQuotationsByLeadIdAsync(Guid leadId)
    {
        var quotations = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l!.Client)
            .Include(q => q.Advisor)
            .Include(q => q.Lot)
            .ThenInclude(l => l!.Block)
            .ThenInclude(b => b.Project)
            .Where(q => q.LeadId == leadId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();

        return quotations.Select(QuotationDTO.FromEntity);
    }

    public async Task<IEnumerable<QuotationSummaryDTO>> GetQuotationsByAdvisorIdAsync(
        Guid advisorId
    )
    {
        var quotations = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l!.Client)
            .Include(q => q.Lot) // **NUEVO: Incluir el lote**
            .ThenInclude(l => l!.Block)
            .ThenInclude(b => b.Project)
            .Where(q => q.AdvisorId == advisorId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();

        return quotations.Select(QuotationSummaryDTO.FromEntity);
    }

    // **NUEVO: Obtener cotizaciones por lote**
    public async Task<IEnumerable<QuotationDTO>> GetQuotationsByLotIdAsync(Guid lotId)
    {
        var quotations = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l!.Client)
            .Include(q => q.Advisor)
            .Include(q => q.Lot)
            .ThenInclude(l => l!.Block)
            .ThenInclude(b => b.Project)
            .Where(q => q.LotId == lotId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();

        return quotations.Select(QuotationDTO.FromEntity);
    }

    // **NUEVO: Obtener cotizaciones por proyecto**
    public async Task<IEnumerable<QuotationDTO>> GetQuotationsByProjectIdAsync(Guid projectId)
    {
        var quotations = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l!.Client)
            .Include(q => q.Advisor)
            .Include(q => q.Lot)
            .ThenInclude(l => l!.Block)
            .ThenInclude(b => b.Project)
            .Where(q => q.Lot!.Block.ProjectId == projectId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();

        return quotations.Select(QuotationDTO.FromEntity);
    }

    public async Task<QuotationDTO> CreateQuotationAsync(QuotationCreateDTO dto)
    {
        // Verificar que el lead existe
        var lead = await _context.Leads.FindAsync(dto.LeadId);
        if (lead == null)
            throw new InvalidOperationException($"Lead con ID {dto.LeadId} no encontrado");

        // Verificar que el asesor existe
        var advisor = await _context.Users.FindAsync(dto.AdvisorId);
        if (advisor == null)
            throw new InvalidOperationException($"Asesor con ID {dto.AdvisorId} no encontrado");

        // **NUEVO: Validar que el lote existe y está disponible**
        var lot = await _context
            .Lots.Include(l => l.Block)
            .ThenInclude(b => b.Project)
            .FirstOrDefaultAsync(l => l.Id == dto.LotId);

        if (lot == null)
            throw new InvalidOperationException($"Lote con ID {dto.LotId} no encontrado");

        if (lot.Status != LotStatus.Available)
            throw new InvalidOperationException(
                $"El lote no está disponible para cotizar. Estado actual: {lot.Status}"
            );

        if (!lot.IsActive || !lot.Block.IsActive || !lot.Block.Project.IsActive)
            throw new InvalidOperationException("El lote, bloque o proyecto no está activo");

        // **NUEVO: Verificar que no hay una cotización activa para este lote**
        var activeQuotation = await _context.Quotations.FirstOrDefaultAsync(q =>
            q.LotId == dto.LotId && q.Status == QuotationStatus.ISSUED
        );

        if (activeQuotation != null)
            throw new InvalidOperationException("Ya existe una cotización activa para este lote");

        // Generar código automáticamente
        var code = await GenerateQuotationCodeAsync();

        // **NUEVO: Crear cotización con datos del lote**
        var quotation = dto.ToEntity(code, lot);
        _context.Quotations.Add(quotation);

        // **NUEVO: Cambiar estado del lote a Quoted**
        lot.Status = LotStatus.Quoted;
        lot.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Recargar la cotización con relaciones incluidas
        var createdQuotation = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l!.Client)
            .Include(q => q.Advisor)
            .Include(q => q.Lot)
            .ThenInclude(l => l!.Block)
            .ThenInclude(b => b.Project)
            .FirstAsync(q => q.Id == quotation.Id);

        return QuotationDTO.FromEntity(createdQuotation);
    }

    public async Task<QuotationDTO?> UpdateQuotationAsync(Guid id, QuotationUpdateDTO dto)
    {
        var quotation = await _context
            .Quotations.Include(q => q.Lot) // **NUEVO: Incluir el lote para validaciones**
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quotation == null)
            return null;

        // Si se cambia el asesor, verificar que existe
        if (dto.AdvisorId.HasValue && dto.AdvisorId.Value != quotation.AdvisorId)
        {
            var advisor = await _context.Users.FindAsync(dto.AdvisorId.Value);
            if (advisor == null)
                throw new InvalidOperationException("Asesor no encontrado");
        }

        dto.ApplyTo(quotation);
        await _context.SaveChangesAsync();

        // Recargar la cotización con relaciones incluidas
        var updatedQuotation = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l!.Client)
            .Include(q => q.Advisor)
            .Include(q => q.Lot)
            .ThenInclude(l => l!.Block)
            .ThenInclude(b => b.Project)
            .FirstAsync(q => q.Id == id);

        return QuotationDTO.FromEntity(updatedQuotation);
    }

    public async Task<bool> DeleteQuotationAsync(Guid id)
    {
        var quotation = await _context
            .Quotations.Include(q => q.Lot) // **NUEVO: Incluir el lote**
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quotation == null)
            return false;

        // **NUEVO: Liberar el lote si la cotización estaba activa**
        if (quotation.Status == QuotationStatus.ISSUED && quotation.Lot != null)
        {
            quotation.Lot.Status = LotStatus.Available;
            quotation.Lot.ModifiedAt = DateTime.UtcNow;
        }

        _context.Quotations.Remove(quotation);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<QuotationDTO?> ChangeStatusAsync(Guid id, string status)
    {
        var quotation = await _context
            .Quotations.Include(q => q.Lot) // **NUEVO: Incluir el lote**
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quotation == null || !Enum.TryParse<QuotationStatus>(status, true, out var statusEnum))
            return null;

        var oldStatus = quotation.Status;
        quotation.Status = statusEnum;
        quotation.ModifiedAt = DateTime.UtcNow;

        // **NUEVO: Actualizar estado del lote según el cambio de estado de la cotización**
        if (quotation.Lot != null)
        {
            switch (statusEnum)
            {
                case QuotationStatus.ACCEPTED:
                    // Si se acepta la cotización, el lote pasa a reservado
                    quotation.Lot.Status = LotStatus.Reserved;
                    break;

                case QuotationStatus.CANCELED:
                    // Si se cancela la cotización, el lote vuelve a disponible
                    quotation.Lot.Status = LotStatus.Available;
                    break;

                case QuotationStatus.ISSUED:
                    // Si vuelve a emitida desde cancelada, el lote pasa a cotizado
                    quotation.Lot.Status = LotStatus.Quoted;
                    break;
            }

            quotation.Lot.ModifiedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        // Recargar la cotización con relaciones incluidas
        var updatedQuotation = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l!.Client)
            .Include(q => q.Advisor)
            .Include(q => q.Lot)
            .ThenInclude(l => l!.Block)
            .ThenInclude(b => b.Project)
            .FirstAsync(q => q.Id == id);

        return QuotationDTO.FromEntity(updatedQuotation);
    }

    // **NUEVO: Método para liberar un lote (volver a Available)**
    public async Task<bool> ReleaseLotAsync(Guid quotationId)
    {
        var quotation = await _context
            .Quotations.Include(q => q.Lot)
            .FirstOrDefaultAsync(q => q.Id == quotationId);

        if (quotation?.Lot == null)
            return false;

        quotation.Status = QuotationStatus.CANCELED;
        quotation.Lot.Status = LotStatus.Available;
        quotation.ModifiedAt = DateTime.UtcNow;
        quotation.Lot.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<string> GenerateQuotationCodeAsync()
    {
        // Formato: COT-YYYY-XXXXX donde YYYY es el año actual y XXXXX es un número secuencial
        int year = DateTime.UtcNow.Year;
        var yearPrefix = $"COT-{year}-";

        // Buscar el último código generado este año
        var lastQuotation = await _context
            .Quotations.Where(q => q.Code.StartsWith(yearPrefix))
            .OrderByDescending(q => q.Code)
            .FirstOrDefaultAsync();

        int sequence = 1;
        if (lastQuotation != null)
        {
            // Extraer y aumentar la secuencia
            var parts = lastQuotation.Code.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out int lastSequence))
            {
                sequence = lastSequence + 1;
            }
        }

        return $"{yearPrefix}{sequence:D5}";
    }
}
