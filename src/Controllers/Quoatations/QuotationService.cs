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
            .FirstOrDefaultAsync(q => q.Id == id);

        return quotation != null ? QuotationDTO.FromEntity(quotation) : null;
    }

    public async Task<IEnumerable<QuotationDTO>> GetQuotationsByLeadIdAsync(Guid leadId)
    {
        var quotations = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l!.Client)
            .Include(q => q.Advisor)
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
            .Where(q => q.AdvisorId == advisorId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();

        return quotations.Select(QuotationSummaryDTO.FromEntity);
    }

    public async Task<QuotationDTO> CreateQuotationAsync(QuotationCreateDTO dto)
    {
        // Verificar que el lead existe
        var lead = await _context.Leads.FindAsync(dto.LeadId);
        if (lead == null)
            throw new Exception("Lead no encontrado");

        // Verificar que el asesor existe
        var advisor = await _context.Users.FindAsync(dto.AdvisorId);
        if (advisor == null)
            throw new Exception("Asesor no encontrado");

        // Generar código automáticamente
        var code = await GenerateQuotationCodeAsync();

        var quotation = dto.ToEntity(code);
        _context.Quotations.Add(quotation);
        await _context.SaveChangesAsync();

        // Recargar la cotización con relaciones incluidas
        var createdQuotation = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l!.Client)
            .Include(q => q.Advisor)
            .FirstAsync(q => q.Id == quotation.Id);

        return QuotationDTO.FromEntity(createdQuotation);
    }

    public async Task<QuotationDTO?> UpdateQuotationAsync(Guid id, QuotationUpdateDTO dto)
    {
        var quotation = await _context.Quotations.FindAsync(id);
        if (quotation == null)
            return null;

        // Ya no verificamos el código ya que se eliminó del DTO de actualización

        // Si se cambia el asesor, verificar que existe
        if (dto.AdvisorId.HasValue && dto.AdvisorId.Value != quotation.AdvisorId)
        {
            var advisor = await _context.Users.FindAsync(dto.AdvisorId.Value);
            if (advisor == null)
                throw new Exception("Asesor no encontrado");
        }

        dto.ApplyTo(quotation);
        await _context.SaveChangesAsync();

        // Recargar la cotización con relaciones incluidas
        var updatedQuotation = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l!.Client)
            .Include(q => q.Advisor)
            .FirstAsync(q => q.Id == id);

        return QuotationDTO.FromEntity(updatedQuotation);
    }

    public async Task<bool> DeleteQuotationAsync(Guid id)
    {
        var quotation = await _context.Quotations.FindAsync(id);
        if (quotation == null)
            return false;

        _context.Quotations.Remove(quotation);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<QuotationDTO?> ChangeStatusAsync(Guid id, string status)
    {
        var quotation = await _context.Quotations.FindAsync(id);
        if (quotation == null || !Enum.TryParse<QuotationStatus>(status, true, out var statusEnum))
            return null;

        quotation.Status = statusEnum;
        quotation.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Recargar la cotización con relaciones incluidas
        var updatedQuotation = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l!.Client)
            .Include(q => q.Advisor)
            .FirstAsync(q => q.Id == id);

        return QuotationDTO.FromEntity(updatedQuotation);
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
