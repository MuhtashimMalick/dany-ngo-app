using NgoFund.Domain.Common;
using NgoFund.Domain.Enums;

namespace NgoFund.Domain.Entities;

/// <summary>Immutable audit trail entry — one row per status change. Never updated or deleted.</summary>
public class ApplicationStatusHistory : BaseEntity
{
    public Guid ApplicationId { get; set; }
    public FundApplication Application { get; set; } = null!;

    public ApplicationStatus? FromStatus { get; set; }

    public ApplicationStatus ToStatus { get; set; }

    public string? Remarks { get; set; }

    public DateTimeOffset ChangedAt { get; set; }
    public Guid? ChangedBy { get; set; }
}
