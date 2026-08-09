namespace NgoFund.Domain.Enums;

/// <summary>The exact seven statuses from the client scope document.</summary>
public enum ApplicationStatus
{
    Pending,
    UnderReview,
    Approved,
    Rejected,
    Paid,
    PartiallyPaid,
    OnHold
}
