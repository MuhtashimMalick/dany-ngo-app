using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NgoFund.Api.Authorization;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.Donations;

namespace NgoFund.Api.Controllers;

[ApiController]
[Route("api/donations")]
[Authorize]
public class DonationsController(
    IDonationService donationService,
    IValidator<CreateDonationRequest> createValidator,
    IValidator<VoidDonationRequest> voidValidator) : ControllerBase
{
    [HttpGet]
    [HasPermission("donations.view")]
    public async Task<ActionResult<PagedResult<DonationDto>>> GetDonations(
        [FromQuery] PagedQuery query, [FromQuery] Guid? donorId, CancellationToken cancellationToken)
        => Ok(await donationService.GetDonationsAsync(query, donorId, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission("donations.view")]
    public async Task<ActionResult<DonationDto>> GetDonation(Guid id, CancellationToken cancellationToken)
        => Ok(await donationService.GetDonationByIdAsync(id, cancellationToken));

    [HttpPost]
    [HasPermission("donations.create")]
    public async Task<ActionResult<DonationDto>> Create(CreateDonationRequest request, CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await donationService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetDonation), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/void")]
    [HasPermission("donations.void")]
    public async Task<IActionResult> Void(Guid id, VoidDonationRequest request, CancellationToken cancellationToken)
    {
        await voidValidator.ValidateAndThrowAsync(request, cancellationToken);
        await donationService.VoidAsync(id, request, cancellationToken);
        return NoContent();
    }
}
