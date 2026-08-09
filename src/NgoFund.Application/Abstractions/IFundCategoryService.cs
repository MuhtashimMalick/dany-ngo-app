using NgoFund.Contracts.FundCategories;

namespace NgoFund.Application.Abstractions;

public interface IFundCategoryService
{
    Task<IReadOnlyList<FundCategoryDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<FundBalanceDto>> GetBalancesAsync(CancellationToken cancellationToken);

    Task<FundCategoryDto> CreateAsync(CreateFundCategoryRequest request, CancellationToken cancellationToken);

    Task<FundCategoryDto> UpdateAsync(Guid id, UpdateFundCategoryRequest request, CancellationToken cancellationToken);
}
