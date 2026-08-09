using NgoFund.Contracts.Settings;

namespace NgoFund.Application.Abstractions;

public interface IAppSettingService
{
    Task<IReadOnlyList<AppSettingDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<AppSettingDto> UpdateAsync(string key, UpdateAppSettingRequest request, CancellationToken cancellationToken);
}
