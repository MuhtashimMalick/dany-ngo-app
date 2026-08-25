using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.AuditLogs;
using NgoFund.Contracts.Common;
using NgoFund.Domain.Entities;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.Infrastructure.Services;

public class AuditLogService(AppDbContext dbContext) : IAuditLogService
{
    public async Task<PagedResult<ActivityLogDto>> GetActivityLogAsync(PagedQuery query, CancellationToken cancellationToken)
    {
        var feedQuery = BuildFeedQuery(query.Search);

        var totalCount = await feedQuery.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var items = await feedQuery
            .OrderByDescending(l => l.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToDto)
            .ToListAsync(cancellationToken);

        return new PagedResult<ActivityLogDto>(items, totalCount, page, pageSize);
    }

    public async Task<(IAsyncEnumerable<ActivityLogDto> Items, string FileName)> ExportActivityLogAsync(string? search, CancellationToken cancellationToken)
    {
        var feedQuery = BuildFeedQuery(search);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = today;
        var to = today;

        if (await feedQuery.AnyAsync(cancellationToken))
        {
            var earliest = await feedQuery.MinAsync(l => l.OccurredAt, cancellationToken);
            var latest = await feedQuery.MaxAsync(l => l.OccurredAt, cancellationToken);
            from = DateOnly.FromDateTime(earliest.UtcDateTime);
            to = DateOnly.FromDateTime(latest.UtcDateTime);
        }

        var fileName = $"activity-log-{from:yyyy-MM-dd}-{to:yyyy-MM-dd}.jsonl";
        return (StreamAsync(feedQuery, cancellationToken), fileName);
    }

    /// <summary>The client-facing feed's one filter, shared by both reads: <c>Summary IS NOT NULL</c> plus an optional Summary/UserName search.</summary>
    private IQueryable<AuditLog> BuildFeedQuery(string? search)
    {
        var feedQuery = dbContext.AuditLogs.AsNoTracking().Where(l => l.Summary != null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            feedQuery = feedQuery.Where(l => l.Summary!.Contains(term) || (l.UserName != null && l.UserName.Contains(term)));
        }

        return feedQuery;
    }

    private static async IAsyncEnumerable<ActivityLogDto> StreamAsync(IQueryable<AuditLog> feedQuery, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var ordered = feedQuery.OrderByDescending(l => l.OccurredAt).Select(ToDto);
        await foreach (var item in ordered.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return item;
        }
    }

    private static readonly System.Linq.Expressions.Expression<Func<AuditLog, ActivityLogDto>> ToDto =
        l => new ActivityLogDto(l.Id, l.Summary!, l.UserName, l.OccurredAt);
}
