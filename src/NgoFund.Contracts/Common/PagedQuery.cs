namespace NgoFund.Contracts.Common;

public record PagedQuery(int Page = 1, int PageSize = 25, string? Search = null);
