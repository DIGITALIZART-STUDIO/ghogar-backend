using GestionHogar.Dtos;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GestionHogar.Services;

public class QuotationService(DatabaseContext _context) : IQuotationService
{
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
        var quotation = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(lead => lead.Client)
            .Include(q => q.Lead)
            .ThenInclude(lead => lead.AssignedTo)
            .FirstOrDefaultAsync(q => q.Id == quotationId);

        if (quotation is null)
        {
            throw new Exception("Quotation not found");
        }

        var client = quotation.Lead?.Client;
        var exchangeRate = quotation.ExchangeRate;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Header()
                    .Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(2);
                        });

                        table.Cell();
                        table.Cell().AlignCenter().Text("COTIZACIÓN").FontSize(24).ExtraBold();
                        table.Cell();
                    });

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(x =>
                    {
                        x.Spacing(0);

                        // Cliente, Copropietario
                        x.Item()
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                });

                                table.Cell();
                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Grey.Darken1)
                                    .Padding(2)
                                    .AlignCenter()
                                    .Text($"T.C. REFERENCIAL: {exchangeRate.ToString()}");
                            });

                        // Cliente, Copropietario
                        x.Item()
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                });

                                table
                                    .Cell()
                                    .PaddingRight(16)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Darken1)
                                    .PaddingTop(2)
                                    .PaddingBottom(2)
                                    .AlignLeft()
                                    .Text($"Cliente: {client?.Name ?? "-"}");
                                table
                                    .Cell()
                                    .PaddingTop(2)
                                    .PaddingBottom(2)
                                    .AlignLeft()
                                    .Text("DETALLES DE FINANCIAMIENTO")
                                    .Bold();
                                table
                                    .Cell()
                                    .PaddingRight(16)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Darken1)
                                    .PaddingTop(2)
                                    .PaddingBottom(2)
                                    .AlignLeft()
                                    .Text($"Copropietario: {client?.CoOwner ?? "-"}");
                            });

                        // DNI, Celular
                        x.Item()
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(4);
                                    columns.RelativeColumn(5);
                                    columns.RelativeColumn(6);
                                });

                                table
                                    .Cell()
                                    .PaddingRight(16)
                                    .Element(DataCellStyle)
                                    .Text($"DNI: {client?.Dni ?? "-"}");
                                table
                                    .Cell()
                                    .PaddingRight(16)
                                    .Element(DataCellStyle)
                                    .Text($"Celular: {client?.PhoneNumber ?? "-"}");
                                table
                                    .Cell()
                                    .Element(DataCellStyle)
                                    .Text($"Nro de Meses: {quotation.MonthsFinanced}");

                                static IContainer DataCellStyle(IContainer container)
                                {
                                    return container
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Grey.Darken1)
                                        .PaddingTop(2)
                                        .PaddingBottom(2)
                                        .AlignLeft();
                                }
                            });

                        // Email, Proyecto
                        x.Item()
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                });

                                table
                                    .Cell()
                                    .PaddingRight(16)
                                    .Element(DataCellStyle)
                                    .Text($"Email: {client?.Email ?? "-"}");
                                table.Cell().Element(DataCellStyle).Text("Fecha de pago/cuota");

                                table
                                    .Cell()
                                    .PaddingRight(16)
                                    .Element(DataCellStyle)
                                    .Text($"Proyecto: {quotation.ProjectName}");
                                table
                                    .Cell()
                                    .Element(DataCellStyle)
                                    .Text($"Fecha de Cotización: {quotation.QuotationDate}");

                                static IContainer DataCellStyle(IContainer container)
                                {
                                    return container
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Grey.Darken1)
                                        .PaddingTop(2)
                                        .PaddingBottom(2)
                                        .AlignLeft();
                                }
                            });

                        x.Item()
                            .PaddingTop(30)
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
                                        .Text("Mz - Lote");
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
                                            .BorderColor(Colors.Grey.Darken1)
                                            .Background(Colors.White);
                                    }
                                });

                                // Sample data rows with all borders
                                var tableData = new[]
                                {
                                    new
                                    {
                                        Lugar = $"{quotation.Block} - {quotation.LotNumber}",
                                        Area = $"{quotation.Area} m2",
                                        PrecioM2 = $"{quotation.PricePerM2}",
                                        PrecioLista = $"{quotation.TotalPrice}",
                                        PrecioSoles = "S/ 95,250.00",
                                    },
                                };

                                foreach (var row in tableData)
                                {
                                    table.Cell().Element(DataCellStyle).Text(row.Lugar);
                                    table.Cell().Element(DataCellStyle).Text(row.Area);
                                    table.Cell().Element(DataCellStyle).Text(row.PrecioM2);
                                    table.Cell().Element(DataCellStyle).Text(row.PrecioLista);
                                    table.Cell().Element(DataCellStyle).Text(row.PrecioSoles);

                                    static IContainer DataCellStyle(IContainer container)
                                    {
                                        return container
                                            .PaddingVertical(0)
                                            .PaddingHorizontal(0)
                                            .Border(1)
                                            .BorderColor(Colors.Grey.Darken1)
                                            .AlignCenter()
                                            .Padding(5);
                                    }
                                }
                            });

                        // DSC. P.Lista
                        x.Item()
                            .PaddingTop(30)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                });

                                table.Cell();
                                table.Cell();
                                table.Cell();
                                table
                                    .Cell()
                                    .ColumnSpan(2)
                                    .Border(1)
                                    .BorderColor(Colors.Grey.Darken1)
                                    .Padding(5)
                                    .AlignCenter()
                                    .Text("Precio de venta");

                                table
                                    .Cell()
                                    .ColumnSpan(2)
                                    .Element(DataCellStyle)
                                    .AlignCenter()
                                    .Text("DSC P.LISTA");
                                table
                                    .Cell()
                                    .Element(DataCellStyle)
                                    .AlignLeft()
                                    .PaddingLeft(10)
                                    .Text($"$ {quotation.Discount.ToString()}");
                                table
                                    .Cell()
                                    .Element(DataCellStyle)
                                    .AlignLeft()
                                    .PaddingLeft(10)
                                    .Text($"$ {quotation.FinalPrice}");
                                table
                                    .Cell()
                                    .Element(DataCellStyle)
                                    .AlignLeft()
                                    .PaddingLeft(10)
                                    .Text(
                                        $"S/ {(quotation.FinalPrice * exchangeRate).ToString("N2")}"
                                    );

                                table.Cell().ColumnSpan(5).Text("");

                                //
                                // Inicial
                                //
                                var downPaymentDollars =
                                    (quotation.DownPayment / 100) * quotation.FinalPrice;
                                var downPaymentSoles = downPaymentDollars * exchangeRate;
                                table.Cell().Element(DataCellStyleThin).Text("Inicial");
                                table
                                    .Cell()
                                    .Element(DataCellStyleThin)
                                    .AlignRight()
                                    .Text($"{quotation.DownPayment} %");
                                table.Cell();
                                table
                                    .Cell()
                                    .Element(DataCellStyleThin)
                                    .Text($"$ {downPaymentDollars.ToString("N2")}");
                                table
                                    .Cell()
                                    .Element(DataCellStyleThin)
                                    .Text($"S/ {downPaymentSoles.ToString("N2")}");

                                table.Cell().ColumnSpan(5).Text("");

                                //
                                // A Financiar
                                //
                                var financingPercentange = 100 - quotation.DownPayment;
                                var financingAmountDollars =
                                    quotation.FinalPrice - downPaymentDollars;
                                var financingAmountSoles = financingAmountDollars * exchangeRate;
                                table.Cell().Element(DataCellStyleThin).Text("A financiar");
                                table
                                    .Cell()
                                    .Element(DataCellStyleThin)
                                    .AlignRight()
                                    .Text($"{financingPercentange} %");
                                table.Cell();
                                table
                                    .Cell()
                                    .Element(DataCellStyleThin)
                                    .Text($"$ {financingAmountDollars.ToString("N2")}");
                                table
                                    .Cell()
                                    .Element(DataCellStyleThin)
                                    .Text($"S/ {financingAmountSoles.ToString("N2")}");

                                table.Cell().ColumnSpan(5).Text("");

                                //
                                // Cuotas
                                //
                                var monthlyPaymentDollars =
                                    financingAmountDollars / quotation.MonthsFinanced;
                                var monthlyPaymentSoles = monthlyPaymentDollars * exchangeRate;
                                table.Cell();
                                table
                                    .Cell()
                                    .Element(DataCellStyleThin)
                                    .AlignCenter()
                                    .Text(quotation.MonthsFinanced.ToString());
                                table
                                    .Cell()
                                    .Element(DataCellStyleThin)
                                    .AlignCenter()
                                    .Text("Cuotas de");
                                table
                                    .Cell()
                                    .Element(DataCellStyleThin)
                                    .Text($"$ {monthlyPaymentDollars.ToString("N2")}");
                                table
                                    .Cell()
                                    .Element(DataCellStyleThin)
                                    .Text($"S/ {monthlyPaymentSoles.ToString("N2")}");

                                table.Cell().ColumnSpan(5).Text("");
                                table.Cell().ColumnSpan(5).Text("");

                                table.Cell().Element(DataCellStyleThin).Text("Asesor");
                                table
                                    .Cell()
                                    .ColumnSpan(2)
                                    .Element(DataCellStyleThin)
                                    .Text($"{quotation.Lead?.AssignedTo?.Name ?? "-"}");
                                table.Cell().ColumnSpan(2);

                                table.Cell().Element(DataCellStyleThin).Text("Celular");
                                table
                                    .Cell()
                                    .ColumnSpan(2)
                                    .Element(DataCellStyleThin)
                                    .Text($"{quotation.Lead?.AssignedTo?.PhoneNumber ?? "-"}");
                                table.Cell().ColumnSpan(2);

                                table
                                    .Cell()
                                    .ColumnSpan(5)
                                    .PaddingTop(4)
                                    .PaddingBottom(4)
                                    .Text("*Cotizacion válida por 5 días")
                                    .FontColor(Colors.Grey.Darken1);

                                static IContainer DataCellStyle(IContainer container)
                                {
                                    return container
                                        .Border(1)
                                        .BorderColor(Colors.Grey.Darken1)
                                        .Padding(5)
                                        .PaddingTop(8)
                                        .PaddingBottom(8);
                                }
                                static IContainer DataCellStyleThin(IContainer container)
                                {
                                    return container
                                        .Border(1)
                                        .BorderColor(Colors.Grey.Darken1)
                                        .PaddingTop(2)
                                        .PaddingBottom(2)
                                        .PaddingLeft(10)
                                        .PaddingRight(10);
                                }
                            });
                    });

                page.Footer().AlignCenter().Text("-- Logos --");
            });
        });

        return document.GeneratePdf();
    }
}
