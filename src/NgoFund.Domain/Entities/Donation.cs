using NgoFund.Domain.Common;
using NgoFund.Domain.Enums;

namespace NgoFund.Domain.Entities;

/// <summary>
/// A single contribution from a donor into exactly one fund category. Financial record — never
/// soft-deleted; reversed via <see cref="Status"/> = Voided plus a reversal row in
/// <see cref="FundTransaction"/>, so the ledger stays append-only and auditable.
/// </summary>
public class Donation : BaseEntity, IUpdateAuditable
{
    public string DonationNumber { get; set; } = null!;

    public Guid DonorId { get; set; }
    public Donor Donor { get; set; } = null!;

    public Guid FundCategoryId { get; set; }
    public FundCategory FundCategory { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateOnly DonationDate { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public string? BankName { get; set; }

    public string? InstrumentNumber { get; set; }

    public string? Notes { get; set; }

    public DonationStatus Status { get; set; } = DonationStatus.Confirmed;

    public DateTimeOffset? VoidedAt { get; set; }
    public Guid? VoidedBy { get; set; }
    public string? VoidReason { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
