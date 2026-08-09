using NgoFund.Contracts.AuditLogs;
using NgoFund.Contracts.Common;

namespace NgoFund.Application.Abstractions;

public interface IAuditLogService
{
    Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(PagedQuery query, CancellationToken cancellationToken);
}
