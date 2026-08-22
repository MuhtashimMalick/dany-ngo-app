namespace NgoFund.Domain.Exceptions;

/// <summary>
/// Raised whenever a loan agreement, or the application it belongs to, is being edited in a way
/// that its current state no longer allows: cancelling an agreement that already has repayments,
/// or changing an application's approved amount/fund while an <c>Active</c> agreement exists
/// (mirrors <see cref="ApprovedAmountBelowCompletedPaymentsException"/>'s guard pattern).
/// </summary>
public sealed class LoanAgreementLockedException(string reason) : DomainException(reason);
