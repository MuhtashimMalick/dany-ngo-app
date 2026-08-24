using System.Globalization;
using System.Text;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Ledgers;

namespace NgoFund.Infrastructure.Services.Reports;

/// <summary>
/// RFC-4180 CSV for the fund transaction ledger export. Same column order as the on-screen table
/// (see <c>FundTransactionLedgerTable.razor</c>) plus two columns the screen only shows as a row
/// tooltip/badge: Date, Category, No., Reference No., Type, Name, GRN (PKR), OG (PKR), Total (PKR).
/// "Type" is the raw <see cref="FundTransactionLedgerRowDto.ReferenceType"/> (e.g. "Donation",
/// "DonationReversal") so a reversal row is identifiable in the raw data rather than blended in with
/// real transactions. Every text field is quoted and embedded quotes are doubled, since
/// <see cref="FundTransactionLedgerRowDto.PartyName"/> is free-text and can contain commas, quotes,
/// or newlines. A UTF-8 BOM is emitted so Excel renders Urdu/Arabic names correctly.
/// </summary>
public class FundLedgerCsvBuilder : IFundLedgerCsvBuilder
{
    public byte[] Build(IReadOnlyList<FundTransactionLedgerRowDto> rows)
    {
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(',', "Date", "Category", "No.", "Reference No.", "Type", "Name", "GRN (PKR)", "OG (PKR)", "Total (PKR)"));

        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(',',
                Quote(row.TransactionDate.ToString("yyyy-MM-dd")),
                Quote(row.CategoryName ?? ""),
                Quote(row.CaseNumber ?? ""),
                Quote(row.ReferenceNumber),
                Quote(row.ReferenceType),
                Quote(row.PartyName),
                Quote(row.AmountIn?.ToString("F2", CultureInfo.InvariantCulture) ?? ""),
                Quote(row.AmountOut?.ToString("F2", CultureInfo.InvariantCulture) ?? ""),
                Quote(row.RunningBalance.ToString("F2", CultureInfo.InvariantCulture))));
        }

        // Encoding.GetBytes() never emits the preamble (that's GetPreamble()/StreamWriter-only
        // behavior) — prepend it explicitly so the BOM is actually on the wire.
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    private static string Quote(string field) => $"\"{field.Replace("\"", "\"\"")}\"";
}
