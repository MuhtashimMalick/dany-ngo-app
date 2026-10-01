namespace NgoFund.Contracts.Intake;

/// <summary>The whole payload Apps Script POSTs for one form submission. <see cref="FormResponseId"/>
/// (Google Forms' own response id) is the idempotency key — see <c>IGoogleFormIntakeService</c>.
/// <see cref="ApplicationType"/> is the raw option text from the form's "Application Type"
/// question (e.g. "Housing Assistance") — mapped to a category code by <c>GoogleFormSubmissionMapper</c>.</summary>
public record GoogleFormSubmissionRequest(
    string FormResponseId,
    DateTimeOffset SubmittedAt,
    string? RespondentEmail,
    string ApplicationType,
    IReadOnlyList<GoogleFormAnswer> Answers,
    IReadOnlyList<GoogleFormFileManifestEntry> Files);
