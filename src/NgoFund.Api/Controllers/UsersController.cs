using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NgoFund.Api.Authorization;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.Users;

namespace NgoFund.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(
    IUserManagementService userService,
    IValidator<CreateUserRequest> createValidator,
    IValidator<UpdateUserRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    [HasPermission("users.view")]
    public async Task<ActionResult<PagedResult<UserSummaryDto>>> GetUsers([FromQuery] PagedQuery query, CancellationToken cancellationToken)
        => Ok(await userService.GetUsersAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission("users.view")]
    public async Task<ActionResult<UserSummaryDto>> GetUser(Guid id, CancellationToken cancellationToken)
        => Ok(await userService.GetUserByIdAsync(id, cancellationToken));

    [HttpPost]
    [HasPermission("users.create")]
    public async Task<ActionResult<UserSummaryDto>> CreateUser(CreateUserRequest request, CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await userService.CreateUserAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetUser), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("users.edit")]
    public async Task<ActionResult<UserSummaryDto>> UpdateUser(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await userService.UpdateUserAsync(id, request, cancellationToken));
    }

    /// <summary>"Delete" per the scope document — implemented as deactivation, see IUserManagementService.</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission("users.delete")]
    public async Task<IActionResult> DeactivateUser(Guid id, CancellationToken cancellationToken)
    {
        await userService.DeactivateUserAsync(id, cancellationToken);
        return NoContent();
    }
}
