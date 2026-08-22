using NgoFund.Domain.Common;
using NgoFund.Domain.Enums;

namespace NgoFund.Domain.Entities;

/// <summary>
/// A repayment against a <see cref="LoanAgreement"/>'s outstanding principal. Mirrors
/// <see cref="Payment"/> field-for-field (including void semantics) since the two share the same
/// disbursement/reversal shape — a repayment posts a <see cref="Enums.TransactionReferenceType.LoanRepayment"/>
/// Credit to the fund ledger, never a donation. Financial record — never soft-deleted, voided
/// instead (posts a <see cref="Enums.TransactionReferenceType.LoanRepaymentReversal"/> Debit
/// rather than editing/deleting the original row).
/// </summary>
public class LoanRepayment : BaseEntity, IUpdateAuditable
{
    public string RepaymentNumber { get; set; } = null!;

    public Guid LoanAgreementId { get; set; }
    public LoanAgreement LoanAgreement { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateOnly RepaymentDate { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public string? InstrumentNumber { get; set; }

    public string? BankName { get; set; }

    public string ReceivedFromName { get; set; } = null!;

    public string? ReceivedFromCnic { get; set; }

    public string? Notes { get; set; }

    public LoanRepaymentStatus Status { get; set; } = LoanRepaymentStatus.Completed;

    public DateTimeOffset? VoidedAt { get; set; }
    public Guid? VoidedBy { get; set; }
    public string? VoidReason { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
