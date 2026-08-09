using NgoFund.Contracts.Common;
using NgoFund.Contracts.Users;

namespace NgoFund.Application.Abstractions;

public interface IUserManagementService
{
    Task<PagedResult<UserSummaryDto>> GetUsersAsync(PagedQuery query, CancellationToken cancellationToken);

    Task<UserSummaryDto> GetUserByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<UserSummaryDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken);

    Task<UserSummaryDto> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// "Delete" per the scope document, implemented as deactivation (IsActive = false), never a
    /// hard delete — user rows are referenced by CreatedBy/audit log foreign keys throughout the
    /// system and must never disappear.
    /// </summary>
    Task DeactivateUserAsync(Guid id, CancellationToken cancellationToken);
}
