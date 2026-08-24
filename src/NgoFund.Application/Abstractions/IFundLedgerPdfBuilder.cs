using NgoFund.Contracts.Ledgers;

namespace NgoFund.Application.Abstractions;

/// <summary>Renders a fund transaction ledger export as a letterhead PDF financial statement.</summary>
public interface IFundLedgerPdfBuilder
{
    byte[] Build(string fundName, DateOnly fromDate, DateOnly toDate, IReadOnlyList<FundTransactionLedgerRowDto> rows);
}
