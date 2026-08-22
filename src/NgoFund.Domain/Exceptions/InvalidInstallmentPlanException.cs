namespace NgoFund.Domain.Exceptions;

/// <summary>Raised by <see cref="Loans.LoanScheduleCalculator"/> when the requested installment plan is invalid.</summary>
public sealed class InvalidInstallmentPlanException(string reason) : DomainException(reason);
