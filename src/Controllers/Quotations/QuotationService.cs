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

    // FIXME: move to separation module
    public async Task<byte[]> GenerateSeparationPdfAsync(Guid quotationId)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(x =>
                    {
                        // Header information table
                        x.Item()
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(10);
                                    columns.RelativeColumn(22);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(12);
                                });

                                // Row 1
                                table.Cell()
                                    .AlignLeft()
                                    .PaddingVertical(5)
                                    .Text("Nombre del cliente");

                                table.Cell()
                                    .PaddingVertical(5)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Black)
                                    .Text("");

                                table.Cell()
                                    .AlignLeft()
                                    .PaddingVertical(5)
                                    .PaddingLeft(10)
                                    .Text("D.N.I.");

                                table.Cell()
                                    .PaddingVertical(5)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Black)
                                    .Text("");

                                // Row 2
                                table.Cell()
                                    .AlignLeft()
                                    .PaddingVertical(5)
                                    .Text("Nombre del coyuge");

                                table.Cell()
                                    .PaddingVertical(5)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Black)
                                    .Text("");

                                table.Cell()
                                    .AlignLeft()
                                    .PaddingVertical(5)
                                    .PaddingLeft(10)
                                    .Text("D.N.I.");

                                table.Cell()
                                    .PaddingVertical(5)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Black)
                                    .Text("");

                                // Row 3
                                table.Cell()
                                    .AlignLeft()
                                    .PaddingVertical(5)
                                    .Text("Nombre del Co-propietario");

                                table.Cell()
                                    .PaddingVertical(5)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Black)
                                    .Text("");

                                table.Cell()
                                    .AlignLeft()
                                    .PaddingVertical(5)
                                    .PaddingLeft(10)
                                    .Text("D.N.I.");

                                table.Cell()
                                    .PaddingVertical(5)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Black)
                                    .Text("");

                                // Row 4
                                table.Cell()
                                    .AlignLeft()
                                    .PaddingVertical(5)
                                    .Text("Razón social");

                                table.Cell()
                                    .PaddingVertical(5)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Black)
                                    .Text("");

                                table.Cell()
                                    .AlignLeft()
                                    .PaddingVertical(5)
                                    .PaddingLeft(10)
                                    .Text("RUC");

                                table.Cell()
                                    .PaddingVertical(5)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Black)
                                    .Text("");

                                // Row 5
                                table.Cell()
                                    .AlignLeft()
                                    .PaddingVertical(5)
                                    .Text("Dirección");

                                table.Cell()
                                    .ColumnSpan(3)
                                    .PaddingVertical(5)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Black)
                                    .Text("");

                            });

                        // Payment method table
                        x.Item()
                            .PaddingTop(20)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(3);
                                });

                                table.Cell()
                                    .AlignLeft()
                                    .PaddingVertical(5)
                                    .Text("EFECTIVO").Bold();

                                table.Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .PaddingVertical(5)
                                    .Text("");

                                table.Cell()
                                    .AlignLeft()
                                    .PaddingVertical(5)
                                    .PaddingLeft(10)
                                    .Text("DEPOSITO").Bold();

                                table.Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .PaddingVertical(5)
                                    .Text("");

                                table.Cell()
                                    .AlignRight()
                                    .PaddingVertical(5)
                                    .PaddingHorizontal(10)
                                    .PaddingLeft(10)
                                    .Text("Banco");

                                table.Cell()
                                    .PaddingVertical(5)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Black)
                                    .Text("");
                            });

                        x.Item()
                            .PaddingTop(20)
                            .Text("Por el concepto de separación del");

                        // Custom table with spans
                        x.Item()
                            .PaddingTop(5)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(4);
                                    columns.RelativeColumn(5);
                                    columns.RelativeColumn(5);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(5);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(5);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(5);
                                });

                                // First row with spans: 1, 3, 1, 1, 1, 1, 1, 1
                                table.Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .AlignCenter()
                                    .Text("Proyecto").Bold();

                                table.Cell()
                                    .ColumnSpan(3)
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .Text("");

                                table.Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .AlignCenter()
                                    .Text("Etapa").Bold();

                                table.Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .Text("");

                                table.Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .AlignCenter()
                                    .Text("Mz.").Bold();

                                table.Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .Text("");

                                table.Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .AlignCenter()
                                    .Text("N lote").Bold();

                                table.Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .Text("");

                                // Second row with spans: 1, 2, 1, 6 (remaining)
                                table.Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .AlignCenter()
                                    .Text("Area").Bold();

                                table.Cell()
                                    .ColumnSpan(2)
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .Text("");

                                table.Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .AlignCenter()
                                    .Text("Precio").Bold();

                                table.Cell()
                                    .ColumnSpan(6)
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .Text("");
                            });


                        x.Item()
                            .PaddingTop(20)
                            .Text("Declaración Jurada: El cliente por el presente declara que ha sido informado verbalmente por la empresa sobre el lote a adquirir, así como los datos de la empresa (RUC, Partida, Representante).");

                        // Numbered text blocks
                        x.Item()
                            .PaddingTop(10)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(30); // Fixed width for numbers
                                    columns.RelativeColumn(1);  // Flexible width for text
                                });

                                // Item 1
                                table.Cell()
                                    .AlignLeft()
                                    .PaddingRight(10)
                                    .Text("1.");

                                table.Cell()
                                    .AlignLeft()
                                    .Text("Este documento es único y exclusivamente válido para la separación de un lote y tiene la condición de arras.");

                                // Item 2
                                table.Cell()
                                    .AlignLeft()
                                    .PaddingRight(10)
                                    .PaddingTop(5)
                                    .Text("2.");

                                table.Cell()
                                    .AlignLeft()
                                    .PaddingTop(5)
                                    .Text("Este documento tiene la condición de arras de acuerdo al Art. 1478 del Código Civil, por lo cual el cliente tiene un plazo de 3 días naturales de realizar el depósito de la inicial acordada en el lota de venta, para suscribir al respectivo contrato de compraventa de bien futuro, vencido dicho plazo y sin producirse dicho depósito se entenderá que el cliente se retracta de la compra perdiendo las arras entregadas a favor de la empresa.");

                                // Item 3
                                table.Cell()
                                    .AlignLeft()
                                    .PaddingRight(10)
                                    .PaddingTop(5)
                                    .Text("3.");

                                table.Cell()
                                    .AlignLeft()
                                    .PaddingTop(5)
                                    .Text("Si fuese la empresa quien se negase injustificadamente a suscribir el contrato de compraventa dentro del plazo establecido previamente, deberá devolver al cliente el arras que le fueron entregadas.");

                                // Item 4
                                table.Cell()
                                    .AlignLeft()
                                    .PaddingRight(10)
                                    .PaddingTop(5)
                                    .Text("4.");

                                table.Cell()
                                    .AlignLeft()
                                    .PaddingTop(5)
                                    .Text("Todos los trámites relacionados a este documento y al contrato que pudieran firmarse en base al mismo son personales y deberán efectuarse únicamente en las oficinas de la empresa.");
                            });


                        // Signature section
                        x.Item()
                            .PaddingTop(80)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                });

                                // Signature labels with top border
                                table.Cell()
                                    .PaddingLeft(40)
                                    .PaddingRight(60)
                                    .BorderTop(1)
                                    .BorderColor(Colors.Black)
                                    .AlignCenter()
                                    .PaddingTop(5)
                                    .BorderColor(Colors.Black)
                                    .Text("LA EMPRESA")
                                    .Bold();

                                table.Cell()
                                    .PaddingLeft(60)
                                    .PaddingRight(40)
                                    .BorderTop(1)
                                    .BorderColor(Colors.Black)
                                    .AlignCenter()
                                    .PaddingTop(5)
                                    .Text("EL CLIENTE")
                                    .Bold();
                            });
                    });
            });
        });

        return (document.GeneratePdf());
    }
}
