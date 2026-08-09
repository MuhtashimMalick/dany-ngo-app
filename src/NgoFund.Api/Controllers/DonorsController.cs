using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NgoFund.Api.Authorization;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.Donors;

namespace NgoFund.Api.Controllers;

[ApiController]
[Route("api/donors")]
[Authorize]
public class DonorsController(
    IDonorService donorService,
    IValidator<CreateDonorRequest> createValidator,
    IValidator<UpdateDonorRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    [HasPermission("donors.view")]
    public async Task<ActionResult<PagedResult<DonorDto>>> GetDonors([FromQuery] PagedQuery query, [FromQuery] bool? activeOnly, CancellationToken cancellationToken)
        => Ok(await donorService.GetDonorsAsync(query, activeOnly, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission("donors.view")]
    public async Task<ActionResult<DonorDto>> GetDonor(Guid id, CancellationToken cancellationToken)
        => Ok(await donorService.GetDonorByIdAsync(id, cancellationToken));

    [HttpPost]
    [HasPermission("donors.create")]
    public async Task<ActionResult<DonorDto>> Create(CreateDonorRequest request, CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await donorService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetDonor), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("donors.edit")]
    public async Task<ActionResult<DonorDto>> Update(Guid id, UpdateDonorRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await donorService.UpdateAsync(id, request, cancellationToken));
    }

    /// <summary>"Delete" per the scope document — implemented as deactivation, see IDonorService.</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission("donors.delete")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await donorService.DeactivateAsync(id, cancellationToken);
        return NoContent();
    }
}
