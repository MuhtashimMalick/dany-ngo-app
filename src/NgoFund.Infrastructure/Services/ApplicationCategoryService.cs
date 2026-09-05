using Microsoft.EntityFrameworkCore;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.ApplicationCategories;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;
using NgoFund.Domain.Exceptions;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.Infrastructure.Services;

public class ApplicationCategoryService(AppDbContext dbContext) : IApplicationCategoryService
{
    public async Task<IReadOnlyList<ApplicationCategoryDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var entities = await dbContext.ApplicationCategories
            .AsNoTracking()
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync(cancellationToken);

        return entities.Select(Map).ToList();
    }

    public async Task<ApplicationCategoryDto> CreateAsync(CreateApplicationCategoryRequest request, CancellationToken cancellationToken)
    {
        if (await dbContext.ApplicationCategories.AnyAsync(c => c.Code == request.Code, cancellationToken))
        {
            throw new DuplicateFieldException("application category", "code", request.Code);
        }

        var entity = new ApplicationCategory
        {
            Code = request.Code,
            Name = request.Name,
            FundEligibility = Enum.Parse<FundEligibility>(request.FundEligibility),
            DefaultMaxAmount = request.DefaultMaxAmount,
            IsActive = true,
            DisplayOrder = request.DisplayOrder,
        };

        dbContext.ApplicationCategories.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(entity);
    }

    public async Task<ApplicationCategoryDto> UpdateAsync(Guid id, UpdateApplicationCategoryRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.ApplicationCategories.FindAsync([id], cancellationToken)
            ?? throw new EntityNotFoundException("ApplicationCategory", id);

        entity.Name = request.Name;
        entity.DefaultMaxAmount = request.DefaultMaxAmount;
        entity.IsActive = request.IsActive;
        entity.DisplayOrder = request.DisplayOrder;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(entity);
    }

    private static ApplicationCategoryDto Map(ApplicationCategory c) =>
        new(c.Id, c.Code, c.Name, c.FundEligibility.ToString(), c.DefaultMaxAmount, c.IsActive, c.DisplayOrder,
            c.RequiresGuarantors, c.TermsText, c.TermsVersion);
}
