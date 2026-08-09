using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NgoFund.Api.Authorization;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Settings;

namespace NgoFund.Api.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController(IAppSettingService appSettingService) : ControllerBase
{
    [HttpGet]
    [HasPermission("settings.view")]
    public async Task<ActionResult<IReadOnlyList<AppSettingDto>>> GetAll(CancellationToken cancellationToken)
        => Ok(await appSettingService.GetAllAsync(cancellationToken));

    [HttpPut("{key}")]
    [HasPermission("settings.manage")]
    public async Task<ActionResult<AppSettingDto>> Update(string key, UpdateAppSettingRequest request, CancellationToken cancellationToken)
        => Ok(await appSettingService.UpdateAsync(key, request, cancellationToken));
}
