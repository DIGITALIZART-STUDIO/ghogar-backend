using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GestionHogar.Dtos;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

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

    public async Task<byte[]> GenerateQuotationPdfAsync(Guid quotationId)
    {
        // For now, ignore the quotationId and generate a sample PDF
        // TODO: Later we'll fetch the actual quotation data and use it

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Header()
                    .Text("COTIZACIÓN DE SERVICIOS")
                    .SemiBold()
                    .FontSize(20)
                    .FontColor(Colors.Blue.Medium);

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(x =>
                    {
                        x.Spacing(20);

                        x.Item().Text("Información del Cliente").FontSize(16).SemiBold();

                        x.Item()
                            .Row(row =>
                            {
                                row.RelativeItem()
                                    .Column(col =>
                                    {
                                        col.Item().Text("Nombre: Juan Pérez");
                                        col.Item().Text("Email: juan.perez@email.com");
                                        col.Item().Text("Teléfono: +1234567890");
                                    });

                                row.RelativeItem()
                                    .Column(col =>
                                    {
                                        col.Item().Text($"Fecha: {DateTime.Now:dd/MM/yyyy}");
                                        col.Item().Text("Código: COT-2024-00001");
                                        col.Item().Text("Estado: Pendiente");
                                    });
                            });

                        x.Item().Text("Detalles de la Cotización").FontSize(16).SemiBold();

                        x.Item()
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1); // Lugar
                                    columns.RelativeColumn(1); // Area
                                    columns.RelativeColumn(1); // Precio por m2
                                    columns.RelativeColumn(1); // Precio de lista
                                    columns.RelativeColumn(1); // Precio en soles
                                });

                                // Header row with all borders
                                table.Header(header =>
                                {
                                    header
                                        .Cell()
                                        .Element(HeaderCellStyle)
                                        .AlignCenter()
                                        .Padding(5)
                                        .Text("Lugar");
                                    header
                                        .Cell()
                                        .Element(HeaderCellStyle)
                                        .AlignCenter()
                                        .Padding(5)
                                        .Text("Area");
                                    header
                                        .Cell()
                                        .Element(HeaderCellStyle)
                                        .AlignCenter()
                                        .Padding(5)
                                        .Text("Precio por m²");
                                    header
                                        .Cell()
                                        .Element(HeaderCellStyle)
                                        .AlignCenter()
                                        .Padding(5)
                                        .Text("Precio de lista");
                                    header
                                        .Cell()
                                        .Element(HeaderCellStyle)
                                        .AlignCenter()
                                        .Padding(5)
                                        .Text("Precio en soles");

                                    static IContainer HeaderCellStyle(IContainer container)
                                    {
                                        return container
                                            .DefaultTextStyle(x => x.SemiBold())
                                            .PaddingVertical(0)
                                            .PaddingHorizontal(0)
                                            .Border(1)
                                            .BorderColor(Colors.Black)
                                            .Background(Colors.White);
                                    }
                                });

                                // Sample data rows with all borders
                                var tableData = new[]
                                {
                                    new
                                    {
                                        Lugar = "Lote A-15, Manzana C",
                                        Area = "120.50 m²",
                                        PrecioM2 = "S/ 850.00",
                                        PrecioLista = "S/ 102,425.00",
                                        PrecioSoles = "S/ 95,250.00",
                                    },
                                    new
                                    {
                                        Lugar = "Lote B-08, Manzana D",
                                        Area = "95.75 m²",
                                        PrecioM2 = "S/ 920.00",
                                        PrecioLista = "S/ 88,090.00",
                                        PrecioSoles = "S/ 82,150.00",
                                    },
                                };

                                foreach (var row in tableData)
                                {
                                    table
                                        .Cell()
                                        .Element(DataCellStyle)
                                        .AlignCenter()
                                        .Padding(5)
                                        .Text(row.Lugar);
                                    table
                                        .Cell()
                                        .Element(DataCellStyle)
                                        .AlignCenter()
                                        .Padding(5)
                                        .Text(row.Area);
                                    table
                                        .Cell()
                                        .Element(DataCellStyle)
                                        .AlignCenter()
                                        .Padding(5)
                                        .Text(row.PrecioM2);
                                    table
                                        .Cell()
                                        .Element(DataCellStyle)
                                        .AlignCenter()
                                        .Padding(5)
                                        .Text(row.PrecioLista);
                                    table
                                        .Cell()
                                        .Element(DataCellStyle)
                                        .AlignCenter()
                                        .Padding(5)
                                        .Text(row.PrecioSoles);

                                    static IContainer DataCellStyle(IContainer container)
                                    {
                                        return container
                                            .PaddingVertical(0)
                                            .PaddingHorizontal(0)
                                            .Border(1)
                                            .BorderColor(Colors.Black);
                                    }
                                }
                            });
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Página ");
                        x.CurrentPageNumber();
                    });
            });
        });

        return document.GeneratePdf();
    }
}
