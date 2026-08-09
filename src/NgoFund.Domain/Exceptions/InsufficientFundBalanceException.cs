namespace NgoFund.Domain.Exceptions;

public sealed class InsufficientFundBalanceException(string fundCategoryName, decimal available, decimal requested)
    : DomainException(
        $"Fund '{fundCategoryName}' has a balance of {available:N2}, which is insufficient to cover a payment of {requested:N2}.");
