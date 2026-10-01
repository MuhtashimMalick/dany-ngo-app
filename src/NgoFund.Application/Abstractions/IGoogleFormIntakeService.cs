using NgoFund.Contracts.Documents;
using NgoFund.Contracts.Intake;

namespace NgoFund.Application.Abstractions;

/// <summary>
/// C6: orchestrates one Google Form submission end to end — applicant find-or-create, application
/// create, category details upsert, guarantors, and an internal intake-notes remark — all in ONE
/// DB transaction, reusing the existing applicant/application/details/document services rather
/// than re-implementing their create logic. Every write is attributed to the Google Form Intake
/// system user (see <c>SystemUsers.GoogleFormIntakeUserId</c>) via <see cref="ICurrentUserService"/>,
/// which the "GoogleFormIntake" API-key auth scheme (C7) already arranges for every request on
/// this path.
/// </summary>
public interface IGoogleFormIntakeService
{
    /// <summary>Idempotent on <see cref="GoogleFormSubmissionRequest.FormResponseId"/>: a second
    /// call with the same id (a retried Apps Script POST, or two concurrent posts racing) returns
    /// the existing application with <c>Created=false</c> instead of creating a duplicate.</summary>
    Task<GoogleFormSubmissionResponse> SubmitAsync(GoogleFormSubmissionRequest request, CancellationToken cancellationToken);

    /// <summary>Resolves the application by <see cref="Domain.Entities.FundApplication.ExternalFormReference"/>
    /// (404 if not found — the submission must land first) and maps <paramref name="questionTitle"/>/
    /// <paramref name="questionHelpText"/> to an owner/slot/document type using the same map
    /// <c>GoogleFormSubmissionMapper</c> uses. Idempotent on <paramref name="driveFileId"/>:
    /// a document with that <c>external_file_reference</c> already on file is returned as-is.</summary>
    Task<(DocumentDto Document, bool Created)> UploadDocumentAsync(
        string formResponseId, string questionTitle, string? questionHelpText, string driveFileId,
        string fileName, string contentType, Stream content, CancellationToken cancellationToken);
}
