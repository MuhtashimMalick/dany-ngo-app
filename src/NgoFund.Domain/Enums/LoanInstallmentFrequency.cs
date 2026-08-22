namespace NgoFund.Domain.Enums;

/// <summary>Installment cadence for a loan agreement. Only <see cref="Monthly"/> is supported today, but
/// modelled as a proper enum (rather than a hardcoded assumption) so a future cadence is additive.</summary>
public enum LoanInstallmentFrequency
{
    Monthly
}
