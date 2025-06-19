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
            .Include(q => q.Lot) // Incluir el Lote
            .ThenInclude(l => l!.Block) // Incluir el Bloque relacionado con el Lote
            .ThenInclude(b => b.Project) // Incluir el Proyecto relacionado con el Bloque
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

    public async Task<byte[]> GenerateQuotationPdfAsync(Guid quotationId)
    {
        var quotation = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(lead => lead.Client)
            .Include(q => q.Lot)
            .ThenInclude(lot => lot.Block)
            .ThenInclude(block => block.Project)
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
                                    .Text(text =>
                                    {
                                        text.Span("Copropietario: ");

                                        // Procesar el JSON de co-owners para extraer solo el nombre del primero
                                        string coOwnerName = "-";
                                        if (!string.IsNullOrEmpty(client?.CoOwners))
                                        {
                                            try
                                            {
                                                // Deserializar el JSON a un elemento JsonElement
                                                var coOwners =
                                                    System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                                                        client.CoOwners
                                                    );

                                                // Verificar que sea un array y tenga al menos un elemento
                                                if (
                                                    coOwners.ValueKind
                                                        == System.Text.Json.JsonValueKind.Array
                                                    && coOwners.GetArrayLength() > 0
                                                )
                                                {
                                                    // Obtener el primer elemento
                                                    var firstCoOwner = coOwners[0];

                                                    // Intentar obtener la propiedad "name" del primer co-owner
                                                    if (
                                                        firstCoOwner.TryGetProperty(
                                                            "name",
                                                            out var nameElement
                                                        )
                                                    )
                                                    {
                                                        coOwnerName =
                                                            nameElement.GetString() ?? "-";
                                                    }
                                                }
                                            }
                                            catch
                                            {
                                                // En caso de error al procesar el JSON, dejar el valor por defecto
                                                coOwnerName = "-";
                                            }
                                        }

                                        text.Span(coOwnerName);
                                    });
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
                                    .Text($"Proyecto: {quotation.Lot.Block.Project.Name}");
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
                                        Lugar = $"{quotation.Lot.Block.Name} - {quotation.Lot.LotNumber}",
                                        Area = $"{quotation.AreaAtQuotation} m2",
                                        PrecioM2 = $"{quotation.PricePerM2AtQuotation}",
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
