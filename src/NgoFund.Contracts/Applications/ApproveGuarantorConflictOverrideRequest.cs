namespace NgoFund.Contracts.Applications;

/// <summary>Item 4 (2026-09 feedback): staff explicitly acknowledging that a guarantor's CNIC also
/// appears on another active application and approving proceeding anyway. Posted to
/// <c>POST /api/applications/{id}/guarantors/{guarantorId}/conflict-override</c>.</summary>
public record ApproveGuarantorConflictOverrideRequest(string Reason);
