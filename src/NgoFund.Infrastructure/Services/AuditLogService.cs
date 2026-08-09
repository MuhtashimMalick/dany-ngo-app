using Microsoft.EntityFrameworkCore;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.AuditLogs;
using NgoFund.Contracts.Common;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.Infrastructure.Services;

public class AuditLogService(AppDbContext dbContext) : IAuditLogService
{
    public async Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(PagedQuery query, CancellationToken cancellationToken)
    {
        var logsQuery = dbContext.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            logsQuery = logsQuery.Where(l => l.EntityName.Contains(term) || (l.UserName != null && l.UserName.Contains(term)));
        }

        var totalCount = await logsQuery.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var items = await logsQuery
            .OrderByDescending(l => l.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new AuditLogDto(l.Id, l.UserId, l.UserName, l.Action, l.EntityName, l.EntityId, l.ChangedColumns, l.OccurredAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogDto>(items, totalCount, page, pageSize);
    }
}
