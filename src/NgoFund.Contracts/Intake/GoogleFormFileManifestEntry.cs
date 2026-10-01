namespace NgoFund.Contracts.Intake;

/// <summary>One file-upload answer on the Google Form. The actual bytes are NOT included in the
/// submission payload — they follow separately via <c>POST /api/intake/google-form/submissions/{formResponseId}/documents</c>,
/// one call per file, matched back to this manifest entry by <see cref="DriveFileId"/>.</summary>
public record GoogleFormFileManifestEntry(string QuestionTitle, string? QuestionHelpText, string DriveFileId, string FileName, string MimeType);
