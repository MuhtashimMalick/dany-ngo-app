namespace NgoFund.Contracts.FundCategories;

/// <summary>Maps 1:1 to <c>vw_fund_balances</c> — never a stored balance, always derived from the ledger.</summary>
public record FundBalanceDto(
    Guid FundCategoryId,
    string Code,
    string Name,
    bool IsZakat,
    decimal TotalCollected,
    decimal TotalUtilized,
    decimal Balance,
    decimal TotalRepaid);
