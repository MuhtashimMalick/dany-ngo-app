namespace NgoFund.Contracts.Ledgers;

/// <summary>
/// Accepted <c>format</c> values for
/// <c>GET api/fund-categories/{id}/transaction-ledger/export</c>. Bound straight from the query
/// string via model binding (case-insensitive, e.g. "csv"/"pdf" both match) rather than validated
/// by hand in the controller — an invalid or missing value fails model binding, and
/// <c>[ApiController]</c>'s automatic model validation turns that into the framework's normal
/// <c>application/problem+json</c> 400 response, keeping this endpoint on the same single error
/// mechanism as the rest of the API (see ).
/// </summary>
public enum FundLedgerExportFormat
{
    Csv,
    Pdf,
}
