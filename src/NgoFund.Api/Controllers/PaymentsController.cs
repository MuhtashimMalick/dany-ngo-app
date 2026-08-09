using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NgoFund.Api.Authorization;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.Payments;

namespace NgoFund.Api.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController(
    IPaymentService paymentService,
    IValidator<CreatePaymentRequest> createValidator,
    IValidator<VoidPaymentRequest> voidValidator) : ControllerBase
{
    [HttpGet]
    [HasPermission("payments.view")]
    public async Task<ActionResult<PagedResult<PaymentDto>>> GetPayments(
        [FromQuery] PagedQuery query, [FromQuery] Guid? applicationId, [FromQuery] string? status,
        [FromQuery] string? paymentMethod, [FromQuery] Guid? fundCategoryId, [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo, CancellationToken cancellationToken)
        => Ok(await paymentService.GetPaymentsAsync(query, applicationId, status, paymentMethod, fundCategoryId, dateFrom, dateTo, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission("payments.view")]
    public async Task<ActionResult<PaymentDto>> GetPayment(Guid id, CancellationToken cancellationToken)
        => Ok(await paymentService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [HasPermission("payments.create")]
    public async Task<ActionResult<PaymentDto>> Create(CreatePaymentRequest request, CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await paymentService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetPayment), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/void")]
    [HasPermission("payments.void")]
    public async Task<IActionResult> Void(Guid id, VoidPaymentRequest request, CancellationToken cancellationToken)
    {
        await voidValidator.ValidateAndThrowAsync(request, cancellationToken);
        await paymentService.VoidAsync(id, request, cancellationToken);
        return NoContent();
    }
}
