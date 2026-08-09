using Microsoft.EntityFrameworkCore;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Settings;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Exceptions;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.Infrastructure.Services;

public class AppSettingService(AppDbContext dbContext) : IAppSettingService
{
    public async Task<IReadOnlyList<AppSettingDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var entities = await dbContext.AppSettings.AsNoTracking().OrderBy(s => s.Key).ToListAsync(cancellationToken);
        return entities.Select(Map).ToList();
    }

    public async Task<AppSettingDto> UpdateAsync(string key, UpdateAppSettingRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.AppSettings.SingleOrDefaultAsync(s => s.Key == key, cancellationToken)
            ?? throw new EntityNotFoundException("AppSetting", key);

        if (!entity.IsEditable)
        {
            throw new SettingNotEditableException(key);
        }

        entity.Value = request.Value;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(entity);
    }

    private static AppSettingDto Map(AppSetting s) => new(s.Key, s.Value, s.DataType, s.Description, s.IsEditable);
}
