namespace NgoFund.Contracts.Applications;

/// <summary>Backs the Desktop Applications screen's Google Form badges.
/// <see cref="UnreadGoogleFormCount"/> is the per-user unread notification count: Google Form
/// applications whose <c>CreatedAt</c> (server receive time, not the form's own
/// <c>SubmittedAt</c> — see <c>users.intake_last_seen_at</c>) is newer than the caller's last-seen
/// mark, regardless of status. <see cref="PendingGoogleFormCount"/> is the separate work-queue
/// figure — Google Form applications still in <c>Pending</c> status, unaffected by what's been
/// seen.</summary>
public record IntakeSummaryDto(int UnreadGoogleFormCount, int PendingGoogleFormCount);
