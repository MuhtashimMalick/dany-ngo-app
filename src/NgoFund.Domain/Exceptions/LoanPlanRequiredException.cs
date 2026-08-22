namespace NgoFund.Domain.Exceptions;

/// <summary>
/// Raised by <c>PaymentService.CreateAsync</c> when a payment is about to disburse from a
/// non-Zakat fund but the application has no <c>Active</c> loan agreement yet. Domain-layer twin
/// of the <c>fn_enforce_loan_plan_before_disbursement</c> DB trigger.
/// </summary>
public sealed class LoanPlanRequiredException(string applicationNumber)
    : DomainException($"Application {applicationNumber} draws from a non-Zakat fund and requires an active loan agreement (POST api/loans) before it can accept payments.");
