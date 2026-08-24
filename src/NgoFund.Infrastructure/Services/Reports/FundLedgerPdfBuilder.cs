using System.Globalization;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Ledgers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NgoFund.Infrastructure.Services.Reports;

/// <summary>
/// A letterhead PDF financial statement for a fund's transaction ledger, for auditors/donors.
/// Logo-only letterhead — no organization name is printed anywhere on the page, per the client
/// (the real org name isn't finalized yet and they don't want a wrong one on an official
/// financial document). Same columns as the on-screen ledger (<c>FundTransactionLedgerTable.razor</c>)
/// plus "Ref. No." (the DON-/PAY-/repayment number, a row title-only tooltip on screen). A reversal
/// row (<c>ReferenceType</c> ending in "Reversal") is rendered in italic grey with a "(Reversal)"
/// suffix on the party name, exactly like the on-screen table's distinct styling — it must never
/// look like a real disbursement in a static document. Total GRN/Total OG are straight sums over the
/// filtered rows (matching the on-screen ledger's own arithmetic, which doesn't exclude reversals
/// either); only Closing Balance is the fund's full history, and the footer note says so explicitly.
/// </summary>
public class FundLedgerPdfBuilder : IFundLedgerPdfBuilder
{
    // The API process's own output directory — Assets/report-logo.png is copied there via
    // <CopyToOutputDirectory> in NgoFund.Api.csproj. Infrastructure can't reference Api (wrong
    // dependency direction), but at runtime this DLL is always loaded inside the Api process, so
    // its base directory IS the Api's output directory.
    private static readonly string LogoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "report-logo.png");

    public byte[] Build(string fundName, DateOnly fromDate, DateOnly toDate, IReadOnlyList<FundTransactionLedgerRowDto> rows)
    {
        var totalIn = rows.Sum(r => r.AmountIn ?? 0m);
        var totalOut = rows.Sum(r => r.AmountOut ?? 0m);
        var closingBalance = rows.Count > 0 ? rows[^1].RunningBalance : 0m;
        var generatedAt = DateTimeOffset.Now;
        var logoBytes = File.Exists(LogoPath) ? File.ReadAllBytes(LogoPath) : null;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Calibri));

                page.Header().Column(header =>
                {
                    if (logoBytes is not null)
                    {
                        header.Item().AlignCenter().Height(50).Image(logoBytes).FitHeight();
                        header.Item().PaddingTop(6);
                    }

                    header.Item().AlignCenter().Text($"Fund Ledger Report — {fundName}").FontSize(16).Bold();
                    header.Item().PaddingTop(3).AlignCenter().Text($"{fromDate:dd MMM yyyy} to {toDate:dd MMM yyyy}").FontSize(10).FontColor(Colors.Grey.Darken1);
                    header.Item().PaddingTop(2).AlignCenter().Text($"Generated on {generatedAt:dd MMM yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Medium);
                    header.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(55);
                        columns.RelativeColumn(1.6f);
                        columns.RelativeColumn(1f);
                        columns.RelativeColumn(1.3f);
                        columns.RelativeColumn(2.4f);
                        columns.ConstantColumn(65);
                        columns.ConstantColumn(65);
                        columns.ConstantColumn(70);
                    });

                    table.Header(h =>
                    {
                        HeaderCell(h.Cell(), "Date");
                        HeaderCell(h.Cell(), "Category");
                        HeaderCell(h.Cell(), "No.");
                        HeaderCell(h.Cell(), "Ref. No.");
                        HeaderCell(h.Cell(), "Name");
                        HeaderCell(h.Cell(), "GRN (PKR)", alignRight: true);
                        HeaderCell(h.Cell(), "OG (PKR)", alignRight: true);
                        HeaderCell(h.Cell(), "Total (PKR)", alignRight: true);
                    });

                    for (var i = 0; i < rows.Count; i++)
                    {
                        var row = rows[i];
                        var bg = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                        var isReversal = IsReversal(row);
                        var name = isReversal ? $"{row.PartyName} (Reversal)" : row.PartyName;

                        RowCell(table.Cell(), bg, row.TransactionDate.ToString("yyyy-MM-dd"), isReversal: isReversal);
                        RowCell(table.Cell(), bg, row.CategoryName ?? "—", isReversal: isReversal);
                        RowCell(table.Cell(), bg, row.CaseNumber ?? "—", isReversal: isReversal);
                        RowCell(table.Cell(), bg, row.ReferenceNumber, isReversal: isReversal);
                        RowCell(table.Cell(), bg, name, isReversal: isReversal);
                        RowCell(table.Cell(), bg, row.AmountIn?.ToString("N2", CultureInfo.InvariantCulture) ?? "—", alignRight: true, isReversal: isReversal);
                        RowCell(table.Cell(), bg, row.AmountOut?.ToString("N2", CultureInfo.InvariantCulture) ?? "—", alignRight: true, isReversal: isReversal);
                        RowCell(table.Cell(), bg, row.RunningBalance.ToString("N2", CultureInfo.InvariantCulture), alignRight: true, bold: true, isReversal: isReversal);
                    }
                });

                page.Footer().Column(footer =>
                {
                    footer.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                    footer.Item().PaddingTop(6).Row(row =>
                    {
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("Total GRN: ").SemiBold();
                            t.Span(totalIn.ToString("N2", CultureInfo.InvariantCulture));
                        });
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("Total OG: ").SemiBold();
                            t.Span(totalOut.ToString("N2", CultureInfo.InvariantCulture));
                        });
                        row.RelativeItem().AlignRight().Text(t =>
                        {
                            t.Span("Closing Balance: ").SemiBold();
                            t.Span(closingBalance.ToString("N2", CultureInfo.InvariantCulture)).Bold();
                        });
                    });
                    footer.Item().PaddingTop(6).AlignCenter()
                        .DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Medium))
                        .Text(t =>
                        {
                            t.Span("Page ");
                            t.CurrentPageNumber();
                            t.Span(" of ");
                            t.TotalPages();
                        });
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void HeaderCell(IContainer cell, string text, bool alignRight = false)
    {
        var container = cell.Background(Colors.Blue.Darken2).Padding(5);
        if (alignRight)
        {
            container = container.AlignRight();
        }

        container.Text(text).FontColor(Colors.White).Bold().FontSize(8.5f);
    }

    private static void RowCell(IContainer cell, string background, string text, bool alignRight = false, bool bold = false, bool isReversal = false)
    {
        var container = cell.Background(background).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4);
        if (alignRight)
        {
            container = container.AlignRight();
        }

        var span = container.Text(text).FontSize(8.5f);
        if (isReversal)
        {
            // Mirrors FundTransactionLedgerTable.razor's reversal styling on screen — a reversal is
            // a correction, not a real transaction, and must never read like one in a static PDF.
            span.Italic().FontColor(Colors.Grey.Darken2);
        }

        if (bold)
        {
            span.SemiBold();
        }
    }

    // Mirrors FundTransactionLedgerTable.razor's IsReversal helper exactly.
    private static bool IsReversal(FundTransactionLedgerRowDto row) => row.ReferenceType.EndsWith("Reversal", StringComparison.Ordinal);
}
