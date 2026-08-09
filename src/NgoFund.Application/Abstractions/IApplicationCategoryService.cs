using NgoFund.Contracts.ApplicationCategories;

namespace NgoFund.Application.Abstractions;

public interface IApplicationCategoryService
{
    Task<IReadOnlyList<ApplicationCategoryDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<ApplicationCategoryDto> CreateAsync(CreateApplicationCategoryRequest request, CancellationToken cancellationToken);

    Task<ApplicationCategoryDto> UpdateAsync(Guid id, UpdateApplicationCategoryRequest request, CancellationToken cancellationToken);
}
