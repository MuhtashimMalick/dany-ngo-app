namespace NgoFund.Contracts.Loans;

/// <summary>Single round-trip payload for the application detail view. <c>Agreement</c> is null when
/// the application has no loan (e.g. paid from the Zakat fund). Summary figures mirror what a future
/// <c>vw_loan_balances</c> DB view will provide, derived the same way <see cref="FundCategories.FundBalanceDto"/>
/// derives fund balances from the ledger.</summary>
public record LoanScheduleDto(
    LoanAgreementDto? Agreement,
    IReadOnlyList<LoanInstallmentDto> Installments,
    IReadOnlyList<LoanRepaymentDto> Repayments,
    decimal PrincipalScheduled,
    decimal PrincipalDisbursed,
    decimal TotalRepaid,
    decimal OutstandingBalance,
    /// <summary>Server-computed as <c>OutstandingBalance &lt;= 0 &amp;&amp; PrincipalDisbursed &gt; 0</c> —
    /// deliberately NOT just <c>OutstandingBalance &lt;= 0</c>, so a brand-new, undisbursed agreement
    /// never reads as settled.</summary>
    bool IsSettled,
    DateOnly? NextDueDate,
    decimal OverdueAmount);
