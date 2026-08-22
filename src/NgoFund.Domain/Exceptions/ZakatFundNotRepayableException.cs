namespace NgoFund.Domain.Exceptions;

/// <summary>
/// Raised when a loan agreement or repayment is attempted against a Zakat fund. Zakat-fund
/// payments are never loans — this is the domain-layer twin of the
/// <c>fn_enforce_loan_agreement_fund_eligibility</c>/<c>fn_enforce_loan_repayment_fund_eligibility</c>
/// DB triggers.
/// </summary>
public sealed class ZakatFundNotRepayableException(string fundCategoryName)
    : DomainException($"'{fundCategoryName}' is a Zakat fund and cannot carry a loan (Qard al-Hasan) agreement — only General (non-Zakat) funds are repayable.");
