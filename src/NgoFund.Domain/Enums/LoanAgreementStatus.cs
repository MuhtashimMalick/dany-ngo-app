namespace NgoFund.Domain.Enums;

/// <summary>
/// A loan agreement's stored status. "Settled" is deliberately NOT a member here — per D6 it is
/// always derived (<c>total_repaid &gt;= principal_disbursed</c>), never stored.
/// </summary>
public enum LoanAgreementStatus
{
    Active,
    Cancelled,
    WrittenOff
}
