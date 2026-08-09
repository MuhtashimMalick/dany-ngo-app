using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.Users;
using NgoFund.Domain.Exceptions;
using NgoFund.Infrastructure.Identity;

namespace NgoFund.Infrastructure.Services;

public class UserManagementService(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager) : IUserManagementService
{
    public async Task<PagedResult<UserSummaryDto>> GetUsersAsync(PagedQuery query, CancellationToken cancellationToken)
    {
        var usersQuery = userManager.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            usersQuery = usersQuery.Where(u => u.FullName.Contains(term) || u.Email!.Contains(term));
        }

        var totalCount = await usersQuery.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var users = await usersQuery
            .OrderBy(u => u.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = new List<UserSummaryDto>(users.Count);
        foreach (var user in users)
        {
            items.Add(await MapAsync(user));
        }

        return new PagedResult<UserSummaryDto>(items, totalCount, page, pageSize);
    }

    public async Task<UserSummaryDto> GetUserByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString()) ?? throw new EntityNotFoundException("User", id);
        return await MapAsync(user);
    }

    public async Task<UserSummaryDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (await userManager.FindByEmailAsync(request.Email) is not null)
        {
            throw new DuplicateEmailException(request.Email);
        }

        await EnsureRolesExistAsync(request.Roles);

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
            FullName = request.FullName,
            Designation = request.Designation,
            IsActive = true,
            MustChangePassword = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var createResult = await userManager.CreateAsync(user, request.TemporaryPassword);
        if (!createResult.Succeeded)
        {
            throw new PasswordPolicyViolationException(string.Join(" ", createResult.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRolesAsync(user, request.Roles);

        return await MapAsync(user);
    }

    public async Task<UserSummaryDto> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString()) ?? throw new EntityNotFoundException("User", id);

        await EnsureRolesExistAsync(request.Roles);

        user.FullName = request.FullName;
        user.Designation = request.Designation;
        user.IsActive = request.IsActive;
        await userManager.UpdateAsync(user);

        var currentRoles = await userManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles.Except(request.Roles).ToList();
        var rolesToAdd = request.Roles.Except(currentRoles).ToList();

        if (rolesToRemove.Count > 0)
        {
            await userManager.RemoveFromRolesAsync(user, rolesToRemove);
        }

        if (rolesToAdd.Count > 0)
        {
            await userManager.AddToRolesAsync(user, rolesToAdd);
        }

        return await MapAsync(user);
    }

    public async Task DeactivateUserAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString()) ?? throw new EntityNotFoundException("User", id);
        user.IsActive = false;
        await userManager.UpdateAsync(user);
    }

    private async Task EnsureRolesExistAsync(IEnumerable<string> roleNames)
    {
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                throw new RoleNotFoundException(roleName);
            }
        }
    }

    private async Task<UserSummaryDto> MapAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new UserSummaryDto(user.Id, user.Email!, user.FullName, user.Designation, user.IsActive, user.MustChangePassword, user.LastLoginAt, roles.ToList());
    }
}
