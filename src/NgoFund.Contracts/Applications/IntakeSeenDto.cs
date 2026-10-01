namespace NgoFund.Contracts.Applications;

/// <summary>Result of marking Google Form arrivals seen — see <c>POST api/me/intake-seen</c>.</summary>
public record IntakeSeenDto(int ClearedUnreadCount, DateTimeOffset SeenAt);
