using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GestionHogar.Services;

public class ReservationService : IReservationService
{
    private readonly DatabaseContext _context;

    public ReservationService(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ReservationDto>> GetAllReservationsAsync()
    {
        return await _context
            .Reservations.Include(r => r.Client)
            .Include(r => r.Quotation)
            .Where(r => r.IsActive)
            .Select(r => new ReservationDto
            {
                Id = r.Id,
                ClientId = r.ClientId,
                ClientName = r.Client.DisplayName,
                QuotationId = r.QuotationId,
                QuotationCode = r.Quotation.Code,
                ReservationDate = r.ReservationDate,
                AmountPaid = r.AmountPaid,
                Currency = r.Currency,
                Status = r.Status,
                PaymentMethod = r.PaymentMethod,
                BankName = r.BankName,
                ExchangeRate = r.ExchangeRate,
                ExpiresAt = r.ExpiresAt,
                Notified = r.Notified,
                Schedule = r.Schedule,
                CreatedAt = r.CreatedAt,
                ModifiedAt = r.ModifiedAt,
            })
            .ToListAsync();
    }

    public async Task<ReservationDto?> GetReservationByIdAsync(Guid id)
    {
        return await _context
            .Reservations.Include(r => r.Client)
            .Include(r => r.Quotation)
            .Where(r => r.Id == id && r.IsActive)
            .Select(r => new ReservationDto
            {
                Id = r.Id,
                ClientId = r.ClientId,
                ClientName = r.Client.DisplayName,
                QuotationId = r.QuotationId,
                QuotationCode = r.Quotation.Code,
                ReservationDate = r.ReservationDate,
                AmountPaid = r.AmountPaid,
                Currency = r.Currency,
                Status = r.Status,
                PaymentMethod = r.PaymentMethod,
                BankName = r.BankName,
                ExchangeRate = r.ExchangeRate,
                ExpiresAt = r.ExpiresAt,
                Notified = r.Notified,
                Schedule = r.Schedule,
                CreatedAt = r.CreatedAt,
                ModifiedAt = r.ModifiedAt,
            })
            .FirstOrDefaultAsync();
    }

    public async Task<Reservation> CreateReservationAsync(ReservationCreateDto reservationDto)
    {
        // Verificar que la cotización existe y obtener el lead asociado
        var quotation = await _context
            .Quotations.Include(q => q.Lead)
            .ThenInclude(l => l.Client)
            .FirstOrDefaultAsync(q => q.Id == reservationDto.QuotationId);
        if (quotation == null)
        {
            throw new ArgumentException("La cotización especificada no existe");
        }

        // Verificar que el lead existe
        if (quotation.Lead == null)
        {
            throw new ArgumentException("La cotización no tiene un lead asociado");
        }

        // Verificar que el cliente del lead existe y está activo
        if (quotation.Lead.Client == null || !quotation.Lead.Client.IsActive)
        {
            throw new ArgumentException("El cliente asociado al lead no existe o está inactivo");
        }

        var clientId = quotation.Lead.ClientId!.Value;
        var client = quotation.Lead.Client;

        // Verificar que no exista ya una reserva activa para esta cotización
        var existingReservation = await _context.Reservations.FirstOrDefaultAsync(r =>
            r.QuotationId == reservationDto.QuotationId && r.IsActive
        );
        if (existingReservation != null)
        {
            throw new ArgumentException("Ya existe una reserva activa para esta cotización");
        }

        var reservation = new Reservation
        {
            ClientId = clientId,
            QuotationId = reservationDto.QuotationId,
            ReservationDate = reservationDto.ReservationDate,
            AmountPaid = reservationDto.AmountPaid,
            Currency = reservationDto.Currency,
            PaymentMethod = reservationDto.PaymentMethod,
            BankName = reservationDto.BankName,
            ExchangeRate = reservationDto.ExchangeRate,
            ExpiresAt = reservationDto.ExpiresAt,
            Schedule = reservationDto.Schedule,
            Status = ReservationStatus.ISSUED,
            Notified = false,
            Client = client,
            Quotation = quotation,
        };

        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();
        return reservation;
    }

    public async Task<bool> DeleteReservationAsync(Guid id)
    {
        var reservation = await _context.Reservations.FirstOrDefaultAsync(r =>
            r.Id == id && r.IsActive
        );
        if (reservation == null)
            return false;

        // Borrado lógico
        reservation.IsActive = false;
        reservation.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<ReservationDto>> GetReservationsByClientIdAsync(Guid clientId)
    {
        return await _context
            .Reservations.Include(r => r.Client)
            .Include(r => r.Quotation)
            .Where(r => r.ClientId == clientId && r.IsActive)
            .Select(r => new ReservationDto
            {
                Id = r.Id,
                ClientId = r.ClientId,
                ClientName = r.Client.DisplayName,
                QuotationId = r.QuotationId,
                QuotationCode = r.Quotation.Code,
                ReservationDate = r.ReservationDate,
                AmountPaid = r.AmountPaid,
                Currency = r.Currency,
                Status = r.Status,
                PaymentMethod = r.PaymentMethod,
                BankName = r.BankName,
                ExchangeRate = r.ExchangeRate,
                ExpiresAt = r.ExpiresAt,
                Notified = r.Notified,
                Schedule = r.Schedule,
                CreatedAt = r.CreatedAt,
                ModifiedAt = r.ModifiedAt,
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<ReservationDto>> GetReservationsByQuotationIdAsync(
        Guid quotationId
    )
    {
        return await _context
            .Reservations.Include(r => r.Client)
            .Include(r => r.Quotation)
            .Where(r => r.QuotationId == quotationId && r.IsActive)
            .Select(r => new ReservationDto
            {
                Id = r.Id,
                ClientId = r.ClientId,
                ClientName = r.Client.DisplayName,
                QuotationId = r.QuotationId,
                QuotationCode = r.Quotation.Code,
                ReservationDate = r.ReservationDate,
                AmountPaid = r.AmountPaid,
                Currency = r.Currency,
                Status = r.Status,
                PaymentMethod = r.PaymentMethod,
                BankName = r.BankName,
                ExchangeRate = r.ExchangeRate,
                ExpiresAt = r.ExpiresAt,
                Notified = r.Notified,
                Schedule = r.Schedule,
                CreatedAt = r.CreatedAt,
                ModifiedAt = r.ModifiedAt,
            })
            .ToListAsync();
    }

    public async Task<byte[]> GenerateReservationPdfAsync(Guid reservationId)
    {
        var reservation = await _context
            .Reservations.Include(r => r.Client)
            .Include(r => r.Quotation)
            .ThenInclude(q => q.Lead)
            .ThenInclude(l => l.AssignedTo)
            .FirstOrDefaultAsync(r => r.Id == reservationId);

        if (reservation == null)
        {
            throw new ArgumentException("Reserva no encontrada");
        }

        var client = reservation.Client;
        var quotation = reservation.Quotation;

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
                        // Fechas
                        x.Item()
                            .PaddingVertical(10)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(100);
                                    columns.RelativeColumn(5);
                                    columns.ConstantColumn(100);
                                    columns.RelativeColumn(2);
                                    columns.ConstantColumn(40);
                                    columns.ConstantColumn(30);
                                    columns.RelativeColumn(6);
                                });

                                table
                                    .Cell()
                                    .RowSpan(2)
                                    .Border(1)
                                    .Padding(5)
                                    .Text($"Fecha: {reservation.ReservationDate:dd/MM/yyyy}");

                                table.Cell().RowSpan(2);

                                table
                                    .Cell()
                                    .RowSpan(2)
                                    .Border(1)
                                    .Padding(5)
                                    .Text($"Vence: {reservation.ExpiresAt:dd/MM/yyyy}");

                                table.Cell().RowSpan(2);

                                table.Cell().Text("SOLES");
                                table.Cell().Text("S/.");
                                if (reservation.Currency == Currency.SOLES)
                                {
                                    table
                                        .Cell()
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text($"S/ {reservation.AmountPaid:N2}");
                                }
                                else
                                {
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Black).Text("");
                                }

                                table.Cell().Text("DOLARES");
                                table.Cell().Text("US$");
                                if (reservation.Currency == Currency.DOLARES)
                                {
                                    table
                                        .Cell()
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text($"$ {reservation.AmountPaid:N2}");
                                }
                                else
                                {
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Black).Text("");
                                }
                            });

                        // Header
                        x.Item()
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(64);
                                    columns.ConstantColumn(35);
                                    columns.RelativeColumn(11);
                                    columns.ConstantColumn(45);
                                    columns.RelativeColumn(11);
                                    columns.ConstantColumn(35);
                                    columns.RelativeColumn(12);
                                });

                                // Nombre del cliente
                                {
                                    table
                                        .Cell()
                                        .ColumnSpan(2)
                                        .AlignLeft()
                                        .PaddingVertical(5)
                                        .Text("Nombre del cliente:");

                                    table
                                        .Cell()
                                        .ColumnSpan(3)
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text(client?.Name ?? "");

                                    table
                                        .Cell()
                                        .AlignLeft()
                                        .PaddingVertical(5)
                                        .PaddingLeft(10)
                                        .Text("D.N.I.");

                                    table
                                        .Cell()
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text(client?.Dni ?? "");
                                }

                                // Nombre del conyugue
                                {
                                    table
                                        .Cell()
                                        .ColumnSpan(2)
                                        .AlignLeft()
                                        .PaddingVertical(5)
                                        .Text("Nombre del cónyuge:");

                                    table
                                        .Cell()
                                        .ColumnSpan(3)
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text("");

                                    table
                                        .Cell()
                                        .AlignLeft()
                                        .PaddingVertical(5)
                                        .PaddingLeft(10)
                                        .Text("D.N.I.");

                                    table
                                        .Cell()
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text("");
                                }

                                // Co-propietario
                                {
                                    table
                                        .Cell()
                                        .ColumnSpan(2)
                                        .AlignLeft()
                                        .PaddingVertical(5)
                                        .Text("Nombre del Co-propietario:");

                                    table
                                        .Cell()
                                        .ColumnSpan(3)
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text(client?.CoOwner ?? "");

                                    table
                                        .Cell()
                                        .AlignLeft()
                                        .PaddingVertical(5)
                                        .PaddingLeft(10)
                                        .Text("D.N.I.");

                                    table
                                        .Cell()
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text("");
                                }

                                // Razon social
                                {
                                    table
                                        .Cell()
                                        .ColumnSpan(2)
                                        .AlignLeft()
                                        .PaddingVertical(5)
                                        .Text("Razón social:");

                                    table
                                        .Cell()
                                        .ColumnSpan(3)
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text(client?.CompanyName ?? "");

                                    table
                                        .Cell()
                                        .AlignLeft()
                                        .PaddingVertical(5)
                                        .PaddingLeft(10)
                                        .Text("R.U.C.");

                                    table
                                        .Cell()
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text(client?.Ruc ?? "");
                                }

                                // Direccion
                                {
                                    table.Cell().AlignLeft().PaddingVertical(5).Text("Dirección:");

                                    table
                                        .Cell()
                                        .ColumnSpan(6)
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text(client?.Address ?? "");
                                }

                                // Email
                                {
                                    table.Cell().AlignLeft().PaddingVertical(5).Text("E-mail:");

                                    table
                                        .Cell()
                                        .ColumnSpan(2)
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text(client?.Email ?? "");

                                    table
                                        .Cell()
                                        .AlignLeft()
                                        .PaddingVertical(5)
                                        .PaddingLeft(5)
                                        .Text("Telef. Fijo");

                                    table
                                        .Cell()
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text("");

                                    table
                                        .Cell()
                                        .AlignLeft()
                                        .PaddingVertical(5)
                                        .PaddingLeft(5)
                                        .Text("Celular");

                                    table
                                        .Cell()
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text(client?.PhoneNumber ?? "");
                                }

                                // Importe de separacion
                                {
                                    table
                                        .Cell()
                                        .ColumnSpan(2)
                                        .AlignLeft()
                                        .PaddingVertical(5)
                                        .Text("Importe de Separación");

                                    var currencySymbol =
                                        reservation.Currency == Currency.SOLES ? "S/" : "$";
                                    table
                                        .Cell()
                                        .ColumnSpan(5)
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text($"{currencySymbol} {reservation.AmountPaid:N2}");
                                }
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

                                table.Cell().AlignLeft().PaddingVertical(5).Text("EFECTIVO").Bold();

                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .PaddingVertical(5)
                                    .Text(
                                        reservation.PaymentMethod == PaymentMethod.CASH ? "X" : ""
                                    );

                                table
                                    .Cell()
                                    .AlignLeft()
                                    .PaddingVertical(5)
                                    .PaddingLeft(10)
                                    .Text("DEPOSITO")
                                    .Bold();

                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .PaddingVertical(5)
                                    .Text(
                                        reservation.PaymentMethod == PaymentMethod.BANK_DEPOSIT
                                            ? "X"
                                            : ""
                                    );

                                table
                                    .Cell()
                                    .AlignRight()
                                    .PaddingVertical(5)
                                    .PaddingHorizontal(10)
                                    .PaddingLeft(10)
                                    .Text("Banco");

                                table
                                    .Cell()
                                    .PaddingVertical(5)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Black)
                                    .Text(reservation.BankName ?? "");
                            });

                        x.Item().PaddingTop(20).Text("Por el concepto de separación del");

                        // Custom table with spans
                        x.Item()
                            .PaddingTop(5)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(50);
                                    columns.RelativeColumn(10);
                                    // |Precio
                                    columns.ConstantColumn(40);
                                    // |Etapa
                                    columns.ConstantColumn(40);
                                    columns.RelativeColumn(5);
                                    // |Mz
                                    columns.ConstantColumn(30);
                                    columns.RelativeColumn(7);
                                    // |N lote
                                    columns.ConstantColumn(40);
                                    columns.RelativeColumn(7);
                                });

                                // First row with spans: 1, 3, 1, 1, 1, 1, 1, 1
                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .AlignCenter()
                                    .Text("Proyecto")
                                    .Bold();

                                table
                                    .Cell()
                                    .ColumnSpan(2)
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .Text(quotation?.ProjectName ?? "");

                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .AlignCenter()
                                    .Text("Etapa")
                                    .Bold();

                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .Text("");

                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .AlignCenter()
                                    .Text("Mz.")
                                    .Bold();

                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .Text(quotation?.BlockName ?? "");

                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .AlignCenter()
                                    .Text("N lote")
                                    .Bold();

                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .Text(quotation?.LotNumber ?? "");

                                // Second row with spans: 1, 2, 1, 6 (remaining)
                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .AlignCenter()
                                    .Text("Area")
                                    .Bold();

                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .Text($"{quotation?.AreaAtQuotation ?? 0} m²");

                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .AlignCenter()
                                    .Text("Precio")
                                    .Bold();

                                table
                                    .Cell()
                                    .ColumnSpan(6)
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .Text($"$ {quotation?.FinalPrice ?? 0:N2}");
                            });

                        x.Item()
                            .PaddingTop(20)
                            .Text(
                                "Declaración Jurada: El cliente por el presente declara que ha sido informado verbalmente por la empresa sobre el lote a adquirir, así como los datos de la empresa (RUC, Partida, Representante)."
                            );

                        // Numbered text blocks
                        x.Item()
                            .PaddingTop(10)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(30); // Fixed width for numbers
                                    columns.RelativeColumn(1); // Flexible width for text
                                });

                                // Item 1
                                table.Cell().AlignLeft().PaddingRight(10).Text("1.");

                                table
                                    .Cell()
                                    .AlignLeft()
                                    .Text(
                                        "Este documento es único y exclusivamente válido para la separación de un lote y tiene la condición de arras."
                                    );

                                // Item 2
                                table.Cell().AlignLeft().PaddingRight(10).PaddingTop(5).Text("2.");

                                table
                                    .Cell()
                                    .AlignLeft()
                                    .PaddingTop(5)
                                    .Text(
                                        "Este documento tiene la condición de arras de acuerdo al Art. 1478 del Código Civil, por lo cual el cliente tiene un plazo de 3 días naturales de realizar el depósito de la inicial acordada en el lota de venta, para suscribir al respectivo contrato de compraventa de bien futuro, vencido dicho plazo y sin producirse dicho depósito se entenderá que el cliente se retracta de la compra perdiendo las arras entregadas a favor de la empresa."
                                    );

                                // Item 3
                                table.Cell().AlignLeft().PaddingRight(10).PaddingTop(5).Text("3.");

                                table
                                    .Cell()
                                    .AlignLeft()
                                    .PaddingTop(5)
                                    .Text(
                                        "Si fuese la empresa quien se negase injustificadamente a suscribir el contrato de compraventa dentro del plazo establecido previamente, deberá devolver al cliente el arras que le fueron entregadas."
                                    );

                                // Item 4
                                table.Cell().AlignLeft().PaddingRight(10).PaddingTop(5).Text("4.");

                                table
                                    .Cell()
                                    .AlignLeft()
                                    .PaddingTop(5)
                                    .Text(
                                        "Todos los trámites relacionados a este documento y al contrato que pudieran firmarse en base al mismo son personales y deberán efectuarse únicamente en las oficinas de la empresa."
                                    );
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
                                table
                                    .Cell()
                                    .PaddingLeft(40)
                                    .PaddingRight(60)
                                    .BorderTop(1)
                                    .BorderColor(Colors.Black)
                                    .AlignCenter()
                                    .PaddingTop(5)
                                    .BorderColor(Colors.Black)
                                    .Text("LA EMPRESA")
                                    .Bold();

                                table
                                    .Cell()
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

        return document.GeneratePdf();
    }
}
