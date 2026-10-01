using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NgoFund.Api.Authorization;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Applications;

namespace NgoFund.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public class MeController(IFundApplicationService applicationService) : ControllerBase
{
    /// <summary>Marks Google Form arrivals seen for the caller — advances their
    /// <c>intake_last_seen_at</c> high-water mark to now.</summary>
    [HttpPost("intake-seen")]
    [HasPermission("applications.view")]
    public async Task<ActionResult<IntakeSeenDto>> MarkIntakeSeen(CancellationToken cancellationToken)
        => Ok(await applicationService.MarkIntakeSeenAsync(cancellationToken));
}
