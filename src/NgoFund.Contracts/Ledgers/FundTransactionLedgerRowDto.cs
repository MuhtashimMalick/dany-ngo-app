namespace NgoFund.Contracts.Ledgers;

/// <summary>
/// One row of a fund's chronological, running-balance ledger (client feedback: Date / Category /
/// No. / Name / GRN (money in) / OG (money out) / Total). A direct projection of
/// <c>fund_transactions</c> IN FULL — every reference type, including reversals — not a
/// donations/payments UNION, so <see cref="RunningBalance"/> on the last row always agrees exactly
/// with <c>vw_fund_balances.balance</c> for the same fund. See
/// <see cref="NgoFund.Application.Abstractions.IFundTransactionLedgerService"/>.
/// </summary>
public record FundTransactionLedgerRowDto(
    DateOnly TransactionDate,
    /// <summary>The raw <c>TransactionReferenceType</c> enum name — "Donation" | "Payment" |
    /// "DonationReversal" | "PaymentReversal" | "Adjustment" | "OpeningBalance" | "LoanRepayment" |
    /// "LoanRepaymentReversal".</summary>
    string ReferenceType,
    /// <summary>Application category name for payment-ish rows; null for donation-ish rows.</summary>
    string? CategoryName,
    /// <summary>The application_number for payment-ish rows (the client's "No." column); null otherwise.</summary>
    string? CaseNumber,
    /// <summary>The event's own number: DON-…, PAY-…, or the loan repayment number.</summary>
    string ReferenceNumber,
    /// <summary>Donor name for donation-ish credits, applicant name for payment-ish rows.</summary>
    string PartyName,
    /// <summary>GRN — the amount when Direction = Credit, else null.</summary>
    decimal? AmountIn,
    /// <summary>OG — the amount when Direction = Debit, else null.</summary>
    decimal? AmountOut,
    /// <summary>Total — the running balance after this row, over the fund's entire history.</summary>
    decimal RunningBalance,
    string? Description);
