using NgoFund.Domain.Common;
using NgoFund.Domain.Enums;

namespace NgoFund.Domain.Entities;

/// <summary>
/// One append-only ledger entry. This is the ONLY source of truth for fund balances in the
/// system — there is deliberately no mutable balance column anywhere. Every donation posts a
/// Credit, every payment posts a Debit, in the same DB transaction as the source row. Voiding
/// posts a reversal row rather than mutating or deleting a past entry.
///
/// (reference_type, reference_id) is unique at the database level, which makes posting
/// idempotent: the same donation/payment can never be posted to the ledger twice.
///
/// Deliberately has no <see cref="IUpdateAuditable"/> or <see cref="ISoftDeletable"/> — this
/// table is never updated and never soft-deleted.
/// </summary>
public class FundTransaction : BaseEntity
{
    public Guid FundCategoryId { get; set; }
    public FundCategory FundCategory { get; set; } = null!;

    public TransactionDirection Direction { get; set; }

    public decimal Amount { get; set; }

    public DateOnly TransactionDate { get; set; }

    public TransactionReferenceType ReferenceType { get; set; }

    /// <summary>Id of the Donation/Payment/etc. row that caused this posting.</summary>
    public Guid ReferenceId { get; set; }

    public string? Description { get; set; }
}
