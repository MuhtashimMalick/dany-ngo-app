using NgoFund.Contracts.AuditLogs;
using NgoFund.Contracts.Common;

namespace NgoFund.Application.Abstractions;

public interface IAuditLogService
{
    /// <summary>The client-facing activity feed — every row with a non-null <c>Summary</c>, searched by Summary/UserName.</summary>
    Task<PagedResult<ActivityLogDto>> GetActivityLogAsync(PagedQuery query, CancellationToken cancellationToken);

    /// <summary>Same feed/filter as <see cref="GetActivityLogAsync"/>, unpaginated and streamed, plus a server-computed NDJSON file name covering the actual date range of the exported rows.</summary>
    Task<(IAsyncEnumerable<ActivityLogDto> Items, string FileName)> ExportActivityLogAsync(string? search, CancellationToken cancellationToken);
}
