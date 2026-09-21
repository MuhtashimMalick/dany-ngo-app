using NgoFund.Domain.Common;
using NgoFund.Domain.Enums;

namespace NgoFund.Domain.Entities;

/// <summary>
/// A disbursement against an approved application. <see cref="FundCategoryId"/> is copied from
/// the application at payment time so the ledger stays correct even if the application's fund
/// assignment is edited later. Financial record — never soft-deleted, voided instead.
/// </summary>
public class Payment : BaseEntity, IUpdateAuditable
{
    public string PaymentNumber { get; set; } = null!;

    public Guid ApplicationId { get; set; }
    public FundApplication Application { get; set; } = null!;

    public Guid FundCategoryId { get; set; }
    public FundCategory FundCategory { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateOnly PaymentDate { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public string? InstrumentNumber { get; set; }

    public string? BankName { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Completed;

    public DateTimeOffset? VoidedAt { get; set; }
    public Guid? VoidedBy { get; set; }
    public string? VoidReason { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
