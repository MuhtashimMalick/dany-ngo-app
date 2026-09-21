namespace NgoFund.Contracts.Applications;

/// <summary>
/// <see cref="ApprovedAmount"/> (item 1, 2026-09 feedback) is only meaningful when
/// <see cref="NewStatus"/> is <c>Approved</c> — it's ignored for every other target status. It may
/// be omitted (null) when re-approving an application from <c>OnHold</c> that already has an
/// approved amount on file, in which case the existing value is kept; it is required the first time
/// an application is approved.
/// </summary>
public record ChangeApplicationStatusRequest(string NewStatus, string? Remarks, string? RejectionReason, decimal? ApprovedAmount = null);
