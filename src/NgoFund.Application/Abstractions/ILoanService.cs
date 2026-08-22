using NgoFund.Contracts.Common;
using NgoFund.Contracts.Loans;

namespace NgoFund.Application.Abstractions;

/// <summary>
/// Owns the full qard al-hasan (interest-free loan) lifecycle — agreement authoring, schedule
/// preview, cancel/write-off, and repayment recording/voiding — as one service rather than three,
/// since they all operate on the same aggregate (a loan agreement and its installments/repayments).
/// </summary>
public interface ILoanService
{
    Task<PagedResult<LoanAgreementDto>> GetLoansAsync(
        PagedQuery query, string? status, bool? overdueOnly, Guid? fundCategoryId, CancellationToken cancellationToken);

    Task<LoanAgreementDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Returns <c>Agreement: null</c> (not a 404) when the application has no loan agreement yet — e.g. Zakat-fund applications never have one. 404s only when the application itself doesn't exist.</summary>
    Task<LoanScheduleDto> GetScheduleByApplicationAsync(Guid applicationId, CancellationToken cancellationToken);

    /// <summary>Calculator-only preview of an installment plan — no DB writes.</summary>
    Task<IReadOnlyList<LoanInstallmentDto>> PreviewScheduleAsync(PreviewLoanScheduleRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Validates the application is Approved with an approved amount set, its fund is non-Zakat,
    /// and it has no existing Active agreement, then generates and persists the installment
    /// schedule in one transaction. Principal is always read server-side from the application's
    /// approved amount — never accepted from the client.
    /// </summary>
    Task<LoanAgreementDto> CreateAgreementAsync(CreateLoanAgreementRequest request, CancellationToken cancellationToken);

    /// <summary>Only legal while the agreement is Active and nothing has been repaid against it yet.</summary>
    Task CancelAgreementAsync(Guid id, CancelLoanAgreementRequest request, CancellationToken cancellationToken);

    Task WriteOffAsync(Guid id, WriteOffLoanRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Records a repayment and posts a matching <c>LoanRepayment</c> Credit to the fund ledger in
    /// one transaction, capped so total completed repayments never exceed disbursed principal.
    /// </summary>
    Task<LoanRepaymentDto> RecordRepaymentAsync(RecordLoanRepaymentRequest request, CancellationToken cancellationToken);

    /// <summary>Marks the repayment Voided and posts a reversing Debit (<c>LoanRepaymentReversal</c>) to the fund ledger, in one transaction.</summary>
    Task VoidRepaymentAsync(Guid id, VoidLoanRepaymentRequest request, CancellationToken cancellationToken);
}
