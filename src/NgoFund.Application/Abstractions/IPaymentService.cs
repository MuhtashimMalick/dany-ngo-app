using NgoFund.Contracts.Common;
using NgoFund.Contracts.Payments;

namespace NgoFund.Application.Abstractions;

public interface IPaymentService
{
    Task<PagedResult<PaymentDto>> GetPaymentsAsync(
        PagedQuery query, Guid? applicationId, string? status, string? paymentMethod, Guid? fundCategoryId,
        DateOnly? dateFrom, DateOnly? dateTo, CancellationToken cancellationToken);

    Task<PaymentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Creates the payment and posts a matching Debit to the fund ledger in one transaction,
    /// enforcing: the application must be Approved/PartiallyPaid, the sum of completed payments
    /// must not exceed the approved amount, and the fund must have sufficient balance. The
    /// application's status is auto-updated to PartiallyPaid/Paid as part of the same transaction.
    /// </summary>
    Task<PaymentDto> CreateAsync(CreatePaymentRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Marks the payment Voided and posts a reversing Credit to the fund ledger, then recomputes
    /// the owning application's status, in one transaction.
    /// </summary>
    Task VoidAsync(Guid id, VoidPaymentRequest request, CancellationToken cancellationToken);
}
