namespace NgoFund.Contracts.Intake;

/// <summary><see cref="Created"/> is false when this call resolved to an already-existing
/// application (idempotent replay of the same <c>FormResponseId</c>) — Apps Script doesn't need to
/// distinguish the two, but it's useful in logs/tests.</summary>
public record GoogleFormSubmissionResponse(Guid ApplicationId, string ApplicationNumber, bool Created);
