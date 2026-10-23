using System.Globalization;
using System.Text.Json;
using GestionHogar.Model;
using GestionHogar.Utils;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GestionHogar.Controllers.Reports;

public sealed class CreditStatementPdfFile
{
    public required byte[] Bytes { get; init; }
    public required string FileName { get; init; }
}

internal sealed class CreditStatementPdfModel
{
    public required Contract Contract { get; init; }
    public required List<CreditStatementQuota> Quotas { get; init; }
    public required List<PaymentTransaction> Transactions { get; init; }
}

internal sealed class CreditStatementQuota
{
    public required Payment Payment { get; init; }
    public required decimal AmountPaid { get; init; }
    public required decimal RemainingAmount { get; init; }
    public required bool Paid { get; init; }
}

internal static class CreditStatementPdf
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // Paleta Gestión Hogar (landing: --color-primary / --color-muted-foreground)
    private const string BrandGold = "#FFD314";
    private const string BrandGoldSoft = "#FFF6C8";
    private const string BrandInk = "#393839";

    private static readonly CultureInfo PeruCulture = CultureInfo.GetCultureInfo("es-PE");

    public static byte[] Generate(CreditStatementPdfModel model)
    {
        var contract = model.Contract;
        var client = contract.Client;
        var project = contract.Lot?.Block?.Project;
        var currency = contract.Currency;
        var generatedAt = DateTime.UtcNow.AddHours(-5);

        return Document
            .Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.4f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9).FontColor(BrandInk));

                    page.Header().Column(header =>
                    {
                        header
                            .Item()
                            .Row(row =>
                            {
                                row.RelativeItem()
                                    .Column(col =>
                                    {
                                        col.Item()
                                            .Text(project?.Name ?? "Sin proyecto")
                                            .Bold()
                                            .FontSize(16)
                                            .FontColor(BrandInk);
                                        col.Item()
                                            .Text("Estado de cuenta de crédito")
                                            .FontSize(12)
                                            .FontColor(Colors.Grey.Darken2);
                                    });
                                row.ConstantItem(180)
                                    .AlignRight()
                                    .Column(col =>
                                    {
                                        col.Item().Text($"Contrato {contract.Code}").SemiBold();
                                        col.Item().Text($"Emitido: {generatedAt:dd/MM/yyyy HH:mm}").FontSize(8);
                                    });
                            });
                        header.Item().PaddingTop(8).LineHorizontal(2).LineColor(BrandGold);
                    });

                    page.Content()
                        .PaddingTop(12)
                        .Column(content =>
                        {
                            content.Spacing(10);

                            content.Item().Element(e => SectionTitle(e, "Datos del cliente"));
                            content.Item().Element(e => ClientSection(e, client));

                            content.Item().Element(e => SectionTitle(e, "Datos del crédito"));
                            content
                                .Item()
                                .Element(e => ContractSection(e, contract, project, model.Quotas, currency));

                            content.Item().Element(e => SectionTitle(e, "Cronograma de pagos"));
                            content.Item().Element(e => ScheduleSection(e, model.Quotas, currency));

                            content.Item().Element(e => SectionTitle(e, "Pagos registrados"));
                            content.Item().Element(e => TransactionsSection(e, model.Transactions, currency));
                        });

                    page.Footer()
                        .AlignCenter()
                        .DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Medium))
                        .Text(text =>
                        {
                            text.Span("Página ");
                            text.CurrentPageNumber();
                            text.Span(" de ");
                            text.TotalPages();
                            text.Span("  ·  Gestión Hogar  ·  Documento de uso interno");
                        });
                });
            })
            .GeneratePdf();
    }

    private static void SectionTitle(IContainer container, string title)
    {
        container
            .Background(BrandGold)
            .PaddingVertical(4)
            .PaddingHorizontal(8)
            .Text(title)
            .FontColor(BrandInk)
            .SemiBold()
            .FontSize(10);
    }

    private static void ClientSection(IContainer container, Model.Client? client)
    {
        var displayName =
            client?.Type == ClientType.Juridico
                ? client.CompanyName ?? client.Name ?? "—"
                : client?.Name ?? client?.CompanyName ?? "—";
        var coOwners = ParseCoOwners(client?.CoOwners);

        container.Column(section =>
        {
            section
                .Item()
                .Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(4);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(4);
                    });

                    AddPair(table, "Nombre", displayName);
                    AddPair(table, "Tipo", client?.Type == ClientType.Juridico ? "Jurídico" : "Natural");
                    AddPair(table, "DNI", client?.Dni ?? "—");
                    AddPair(table, "RUC", client?.Ruc ?? "—");
                    AddPair(table, "Teléfono", client?.PhoneNumber ?? "—");
                    AddPair(table, "Correo", client?.Email ?? "—");
                    AddPair(table, "Dirección", client?.Address ?? "—");
                    AddPair(table, "País", client?.Country ?? "—");
                    AddPair(table, "Razón social", client?.CompanyName ?? "—");
                    AddPair(table, "Bienes separados", client?.SeparateProperty == true ? "Sí" : "No");
                });

            if (coOwners.Count == 0)
            {
                return;
            }

            section
                .Item()
                .PaddingTop(6)
                .Column(col =>
                {
                    col.Item().Text("Copropietarios").SemiBold();
                    foreach (var owner in coOwners)
                    {
                        col.Item()
                            .Text(
                                $"{owner.Name ?? "—"}  ·  DNI {owner.Dni ?? "—"}  ·  {owner.Phone ?? "sin teléfono"}  ·  {owner.Email ?? "sin correo"}"
                            )
                            .FontSize(8);
                    }
                });
        });
    }

    private static void ContractSection(
        IContainer container,
        Contract contract,
        Project? project,
        List<CreditStatementQuota> quotas,
        Currency currency
    )
    {
        var origin = contract.Origin switch
        {
            ContractOrigin.Pipeline => "Separación",
            ContractOrigin.Direct => "Directo",
            ContractOrigin.Legacy => "Legado",
            _ => contract.Origin.ToString(),
        };
        var paid = quotas.Sum(q => q.AmountPaid);
        var remaining = quotas.Sum(q => q.RemainingAmount);

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);
                columns.RelativeColumn(4);
                columns.RelativeColumn(2);
                columns.RelativeColumn(4);
            });

            AddPair(table, "Proyecto", project?.Name ?? "—");
            AddPair(table, "Manzana / Lote", $"{contract.Lot?.Block?.Name ?? "—"} / {contract.Lot?.LotNumber ?? "—"}");
            AddPair(table, "Fecha de inicio", contract.StartDate.ToString("dd/MM/yyyy"));
            AddPair(table, "Origen", origin);
            AddPair(table, "Moneda", currency == Currency.DOLARES ? "Dólares (US$)" : "Soles (S/)");
            AddPair(table, "Tipo de cambio", contract.ExchangeRate.ToString("N4", PeruCulture));
            AddPair(table, "Precio total", CurrencyMapping.FormatAmount(contract.TotalPrice, currency));
            AddPair(table, "Inicial", CurrencyMapping.FormatAmount(contract.DownPaymentAmount, currency));
            AddPair(table, "Financiado", CurrencyMapping.FormatAmount(contract.AmountFinanced, currency));
            AddPair(table, "Plazo", $"{contract.MonthsFinanced} meses");
            AddPair(table, "Pagado", CurrencyMapping.FormatAmount(paid, currency));
            AddPair(table, "Saldo pendiente", CurrencyMapping.FormatAmount(remaining, currency));
        });
    }

    private static void ScheduleSection(
        IContainer container,
        List<CreditStatementQuota> quotas,
        Currency currency
    )
    {
        if (quotas.Count == 0)
        {
            container.Text("No hay cronograma de pagos para este crédito.").Italic();
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2.2f);
                columns.RelativeColumn(1.6f);
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn(1.4f);
            });

            AddHeader(table, "Cuota");
            AddHeader(table, "Vence");
            AddHeader(table, "Monto");
            AddHeader(table, "Pagado");
            AddHeader(table, "Pendiente");
            AddHeader(table, "Estado");

            foreach (var quota in quotas)
            {
                var label = quota.Payment.Type switch
                {
                    PaymentType.DownPayment => "Enganche",
                    PaymentType.Extra => "Extra",
                    _ => $"Cuota {quota.Payment.Sequence}",
                };
                var status = quota.Paid
                    ? "Pagado"
                    : quota.AmountPaid > 0
                        ? "Parcial"
                        : quota.Payment.DueDate < DateTime.UtcNow
                            ? "Vencido"
                            : "Pendiente";

                AddBody(table, label);
                AddBody(table, quota.Payment.DueDate.ToString("dd/MM/yyyy"));
                AddBody(table, CurrencyMapping.FormatAmount(quota.Payment.AmountDue, currency));
                AddBody(table, CurrencyMapping.FormatAmount(quota.AmountPaid, currency));
                AddBody(table, CurrencyMapping.FormatAmount(quota.RemainingAmount, currency));
                AddBody(table, status);
            }
        });
    }

    private static void TransactionsSection(
        IContainer container,
        List<PaymentTransaction> transactions,
        Currency currency
    )
    {
        if (transactions.Count == 0)
        {
            container.Text("No hay pagos registrados para este crédito.").Italic();
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.6f);
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn(2);
                columns.RelativeColumn(2.2f);
                columns.RelativeColumn(1.2f);
            });

            AddHeader(table, "Fecha");
            AddHeader(table, "Monto");
            AddHeader(table, "Método");
            AddHeader(table, "Nº operación");
            AddHeader(table, "Cuotas");

            foreach (var transaction in transactions)
            {
                var method = transaction.PaymentMethod switch
                {
                    PaymentMethod.CASH => "Efectivo",
                    PaymentMethod.BANK_DEPOSIT => "Depósito bancario",
                    PaymentMethod.BANK_TRANSFER => "Transferencia bancaria",
                    _ => transaction.PaymentMethod.ToString(),
                };
                var limaDate = transaction.PaymentDate.ToOffset(TimeSpan.FromHours(-5));
                var quotasCovered =
                    transaction.PaymentDetails?.Count > 0
                        ? transaction.PaymentDetails.Count
                        : transaction.Payments?.Count ?? 0;

                AddBody(table, limaDate.ToString("dd/MM/yyyy"));
                AddBody(table, CurrencyMapping.FormatAmount(transaction.AmountPaid, currency));
                AddBody(table, method);
                AddBody(table, string.IsNullOrWhiteSpace(transaction.ReferenceNumber) ? "—" : transaction.ReferenceNumber);
                AddBody(table, quotasCovered.ToString());
            }
        });
    }

    private static void AddPair(TableDescriptor table, string label, string value)
    {
        table
            .Cell()
            .BorderBottom(0.5f)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(3)
            .Text(label)
            .SemiBold()
            .FontSize(8);
        table
            .Cell()
            .BorderBottom(0.5f)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(3)
            .Text(value)
            .FontSize(8);
    }

    private static void AddHeader(TableDescriptor table, string text)
    {
        table
            .Cell()
            .Background(BrandGoldSoft)
            .Padding(4)
            .Text(text)
            .SemiBold()
            .FontSize(8)
            .FontColor(BrandInk);
    }

    private static void AddBody(TableDescriptor table, string text)
    {
        table.Cell().BorderBottom(0.4f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(text).FontSize(8);
    }

    private static List<CoOwnerJson> ParseCoOwners(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<CoOwnerJson>>(raw, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed class CoOwnerJson
    {
        public string? Name { get; set; }
        public string? Dni { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
    }
}
