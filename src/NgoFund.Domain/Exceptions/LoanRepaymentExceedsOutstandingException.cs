namespace NgoFund.Domain.Exceptions;

/// <summary>
/// Raised when a repayment would push total completed repayments above the loan's disbursed
/// principal. Domain-layer twin of the <c>fn_enforce_loan_repayment_cap</c> DB trigger.
/// </summary>
public sealed class LoanRepaymentExceedsOutstandingException(decimal outstandingBalance, decimal attemptedAmount)
    : DomainException(
        $"Repayment of {attemptedAmount:N2} would exceed the outstanding balance of {outstandingBalance:N2}.");
