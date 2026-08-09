namespace NgoFund.Application.Abstractions;

/// <summary>
/// Hands out human-readable, collision-free numbers (DNR-2026-00001, DON-2026-00001, ...) backed
/// by <c>number_sequences</c>. Must be called inside the same DB transaction as the entity it
/// numbers, so a failed save can't leave a gap-free sequence with skipped numbers meaning
/// something.
/// </summary>
public interface INumberGenerator
{
    Task<string> NextAsync(string entityType, CancellationToken cancellationToken);
}
