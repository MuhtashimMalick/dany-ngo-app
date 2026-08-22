using Microsoft.EntityFrameworkCore;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.FundCategories;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Exceptions;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.Infrastructure.Services;

public class FundCategoryService(AppDbContext dbContext) : IFundCategoryService
{
    public async Task<IReadOnlyList<FundCategoryDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var entities = await dbContext.FundCategories
            .AsNoTracking()
            .OrderBy(f => f.DisplayOrder)
            .ToListAsync(cancellationToken);

        return entities.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<FundBalanceDto>> GetBalancesAsync(CancellationToken cancellationToken)
    {
        // No column aliasing needed: EFCore.NamingConventions (snake_case) applies to SqlQuery<T>
        // mapping the same way it does for regular entities, so these raw column names already
        // match FundBalanceDto's properties (fund_category_id -> FundCategoryId, etc.).
        return await dbContext.Database.SqlQuery<FundBalanceDto>(
            $"""
            SELECT fund_category_id, code, name, is_zakat, total_collected, total_utilized, balance, total_repaid
            FROM vw_fund_balances
            ORDER BY name
            """).ToListAsync(cancellationToken);
    }

    public async Task<FundCategoryDto> CreateAsync(CreateFundCategoryRequest request, CancellationToken cancellationToken)
    {
        if (await dbContext.FundCategories.AnyAsync(f => f.Code == request.Code, cancellationToken))
        {
            throw new DuplicateFieldException("fund category", "code", request.Code);
        }

        var entity = new FundCategory
        {
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            IsZakat = request.IsZakat,
            IsActive = true,
            DisplayOrder = request.DisplayOrder,
        };

        dbContext.FundCategories.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(entity);
    }

    public async Task<FundCategoryDto> UpdateAsync(Guid id, UpdateFundCategoryRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.FundCategories.FindAsync([id], cancellationToken)
            ?? throw new EntityNotFoundException("FundCategory", id);

        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.IsActive = request.IsActive;
        entity.DisplayOrder = request.DisplayOrder;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(entity);
    }

    private static FundCategoryDto Map(FundCategory f) =>
        new(f.Id, f.Code, f.Name, f.Description, f.IsZakat, f.IsActive, f.DisplayOrder);
}
