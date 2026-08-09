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
    OpeningBalance
}
