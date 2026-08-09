using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NgoFund.Api.Authorization;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.AuditLogs;
using NgoFund.Contracts.Common;

namespace NgoFund.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize]
public class AuditLogsController(IAuditLogService auditLogService) : ControllerBase
{
    [HttpGet]
    [HasPermission("auditlogs.view")]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> GetAuditLogs([FromQuery] PagedQuery query, CancellationToken cancellationToken)
        => Ok(await auditLogService.GetAuditLogsAsync(query, cancellationToken));
}
