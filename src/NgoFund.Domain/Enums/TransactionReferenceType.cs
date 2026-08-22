namespace NgoFund.Domain.Enums;

/// <summary>
/// What caused a <c>fund_transactions</c> row to be posted. Combined with <c>reference_id</c>,
/// (reference_type, reference_id) is unique — this is what makes ledger posting idempotent.
/// </summary>
public enum TransactionReferenceType
{
    Donation,
    Payment,
    DonationReversal,
    PaymentReversal,
    Adjustment,
    OpeningBalance,

    /// <summary>
    /// A qard al-hasan (interest-free loan) repayment. Deliberately NOT a donation — no donor
    /// row, no donation number, must never appear in donation reports. See D4 in the loans
    /// feature note in docs/schema.md for <c>vw_fund_balances</c>' handling of this type.
    /// </summary>
    LoanRepayment,

    /// <summary>Reversal of a voided <see cref="LoanRepayment"/>.</summary>
    LoanRepaymentReversal
}
