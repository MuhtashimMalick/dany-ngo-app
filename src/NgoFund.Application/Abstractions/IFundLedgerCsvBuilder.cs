using NgoFund.Contracts.Ledgers;

namespace NgoFund.Application.Abstractions;

/// <summary>Renders a fund transaction ledger export as an RFC-4180 CSV file (UTF-8 with BOM).</summary>
public interface IFundLedgerCsvBuilder
{
    byte[] Build(IReadOnlyList<FundTransactionLedgerRowDto> rows);
}
