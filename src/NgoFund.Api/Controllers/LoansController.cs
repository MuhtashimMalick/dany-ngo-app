using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NgoFund.Api.Authorization;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.Loans;

namespace NgoFund.Api.Controllers;

[ApiController]
[Route("api/loans")]
[Authorize]
public class LoansController(
    ILoanService loanService,
    IValidator<PreviewLoanScheduleRequest> previewValidator,
    IValidator<CreateLoanAgreementRequest> createValidator,
    IValidator<CancelLoanAgreementRequest> cancelValidator,
    IValidator<WriteOffLoanRequest> writeOffValidator,
    IValidator<RecordLoanRepaymentRequest> recordRepaymentValidator,
    IValidator<VoidLoanRepaymentRequest> voidRepaymentValidator) : ControllerBase
{
    [HttpGet]
    [HasPermission("loans.view")]
    public async Task<ActionResult<PagedResult<LoanAgreementDto>>> GetLoans(
        [FromQuery] PagedQuery query, [FromQuery] string? status, [FromQuery] bool? overdue,
        [FromQuery] Guid? fundCategoryId, CancellationToken cancellationToken)
        => Ok(await loanService.GetLoansAsync(query, status, overdue, fundCategoryId, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission("loans.view")]
    public async Task<ActionResult<LoanAgreementDto>> GetLoan(Guid id, CancellationToken cancellationToken)
        => Ok(await loanService.GetByIdAsync(id, cancellationToken));

    [HttpGet("by-application/{applicationId:guid}")]
    [HasPermission("loans.view")]
    public async Task<ActionResult<LoanScheduleDto>> GetScheduleByApplication(Guid applicationId, CancellationToken cancellationToken)
        => Ok(await loanService.GetScheduleByApplicationAsync(applicationId, cancellationToken));

    [HttpPost("preview")]
    [HasPermission("loans.manage")]
    public async Task<ActionResult<IReadOnlyList<LoanInstallmentDto>>> Preview(PreviewLoanScheduleRequest request, CancellationToken cancellationToken)
    {
        await previewValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await loanService.PreviewScheduleAsync(request, cancellationToken));
    }

    [HttpPost]
    [HasPermission("loans.manage")]
    public async Task<ActionResult<LoanAgreementDto>> Create(CreateLoanAgreementRequest request, CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await loanService.CreateAgreementAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetLoan), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/cancel")]
    [HasPermission("loans.manage")]
    public async Task<IActionResult> Cancel(Guid id, CancelLoanAgreementRequest request, CancellationToken cancellationToken)
    {
        await cancelValidator.ValidateAndThrowAsync(request, cancellationToken);
        await loanService.CancelAgreementAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/write-off")]
    [HasPermission("loans.writeoff")]
    public async Task<IActionResult> WriteOff(Guid id, WriteOffLoanRequest request, CancellationToken cancellationToken)
    {
        await writeOffValidator.ValidateAndThrowAsync(request, cancellationToken);
        await loanService.WriteOffAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("repayments")]
    [HasPermission("loans.repay")]
    public async Task<ActionResult<LoanRepaymentDto>> RecordRepayment(RecordLoanRepaymentRequest request, CancellationToken cancellationToken)
    {
        await recordRepaymentValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await loanService.RecordRepaymentAsync(request, cancellationToken));
    }

    [HttpPost("repayments/{id:guid}/void")]
    [HasPermission("loans.void")]
    public async Task<IActionResult> VoidRepayment(Guid id, VoidLoanRepaymentRequest request, CancellationToken cancellationToken)
    {
        await voidRepaymentValidator.ValidateAndThrowAsync(request, cancellationToken);
        await loanService.VoidRepaymentAsync(id, request, cancellationToken);
        return NoContent();
    }
}
