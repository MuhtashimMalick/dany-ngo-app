namespace NgoFund.Contracts.Applications;

/// <summary>Backs <c>GET /api/applications/{id}/completeness</c> — the same evaluation the
/// Approved-transition gate runs, exposed read-only so the wizard and manage-view checklist can
/// never disagree with what the gate will actually enforce.</summary>
public record ApplicationCompletenessDto(
    Guid ApplicationId,
    bool IsComplete,
    IReadOnlyList<MissingFieldDto> MissingFields,
    IReadOnlyList<DocumentSlotStatusDto> Slots);
