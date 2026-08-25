using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using NgoFund.Api.Authorization;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.AuditLogs;
using NgoFund.Contracts.Common;

namespace NgoFund.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize]
public class AuditLogsController(IAuditLogService auditLogService, IOptions<JsonOptions> jsonOptions) : ControllerBase
{
    private static readonly byte[] NewLine = "\n"u8.ToArray();

    [HttpGet]
    [HasPermission("auditlogs.view")]
    public async Task<ActionResult<PagedResult<ActivityLogDto>>> GetActivityLog([FromQuery] PagedQuery query, CancellationToken cancellationToken)
        => Ok(await auditLogService.GetActivityLogAsync(query, cancellationToken));

    /// <summary>NDJSON (one JSON object per line) download of the same feed as <see cref="GetActivityLog"/>,
    /// unpaginated and streamed straight to the response body — never buffers the full result set.</summary>
    [HttpGet("export")]
    [HasPermission("auditlogs.view")]
    public async Task Export([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var (items, fileName) = await auditLogService.ExportActivityLogAsync(search, cancellationToken);

        Response.ContentType = "application/x-ndjson";
        Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = fileName }.ToString();

        await foreach (var item in items.WithCancellation(cancellationToken))
        {
            // Same JsonSerializerOptions as every other controller response (camelCase by
            // ASP.NET Core's default) — this endpoint bypasses the MVC formatter pipeline to
            // stream, but the property casing should still match the rest of the API.
            await JsonSerializer.SerializeAsync(Response.Body, item, jsonOptions.Value.JsonSerializerOptions, cancellationToken);
            await Response.Body.WriteAsync(NewLine, cancellationToken);
        }
    }
}
