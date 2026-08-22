using NgoFund.Domain.Common;
using NgoFund.Domain.Enums;
using NgoFund.Domain.Exceptions;

namespace NgoFund.Domain.Entities;

/// <summary>
/// A General-fund application's interest-free (qard al-hasan) repayment plan. <see cref="FundCategoryId"/>
/// is frozen at creation time to the fund the application actually drew payments from (D1) —
/// never the Zakat fund, see <see cref="EnsureFundIsRepayable"/>. <see cref="PrincipalAmount"/> is
/// frozen from the application's approved amount at creation time and is what
/// <see cref="Loans.LoanScheduleCalculator.GenerateInstallments"/> divides across installments;
/// what's actually owed is capped by disbursed principal (D3), computed at read time, never
/// stored here.
/// </summary>
public class LoanAgreement : BaseEntity, IUpdateAuditable
{
    public string LoanNumber { get; set; } = null!;

    public Guid ApplicationId { get; set; }
    public FundApplication Application { get; set; } = null!;

    public Guid FundCategoryId { get; set; }
    public FundCategory FundCategory { get; set; } = null!;

    public decimal PrincipalAmount { get; set; }

    public LoanAgreementStatus Status { get; set; } = LoanAgreementStatus.Active;

    public int InstallmentCount { get; set; }

    public LoanInstallmentFrequency Frequency { get; set; } = LoanInstallmentFrequency.Monthly;

    public DateOnly FirstDueDate { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancelReason { get; set; }

    public DateTimeOffset? WrittenOffAt { get; set; }
    public Guid? WrittenOffBy { get; set; }
    public string? WrittenOffReason { get; set; }

    public ICollection<LoanInstallment> Installments { get; set; } = [];
    public ICollection<LoanRepayment> Repayments { get; set; } = [];

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>
    /// The loan-fund half of D1/D7#1: a loan agreement's fund must never be a Zakat fund. Call
    /// before assigning <see cref="FundCategoryId"/>. Mirrored by the
    /// <c>fn_enforce_loan_agreement_fund_eligibility</c> DB trigger as defence in depth. Sibling
    /// to <see cref="FundApplication.EnsureFundIsCompatible"/>.
    /// </summary>
    public static void EnsureFundIsRepayable(FundCategory fund)
    {
        if (fund.IsZakat)
        {
            throw new ZakatFundNotRepayableException(fund.Name);
        }
    }

    /// <summary>
    /// Cancelling is only legal while nothing has been repaid against this agreement yet — once
    /// money has moved, the agreement is history and must be written off instead (which forgives
    /// the remaining balance rather than pretending the loan never existed). Call before flipping
    /// <see cref="Status"/> to <see cref="LoanAgreementStatus.Cancelled"/>.
    /// </summary>
    public void EnsureCancellable(decimal totalRepaid)
    {
        if (totalRepaid > 0)
        {
            throw new LoanAgreementLockedException(
                $"Cannot cancel loan agreement {LoanNumber}: {totalRepaid:N2} has already been repaid against it.");
        }
    }
}
