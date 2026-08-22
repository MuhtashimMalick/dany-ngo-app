namespace NgoFund.Contracts.Documents;

/// <summary>
/// A base64-encoded file carried inline inside another request, so it can commit atomically with
/// the row that owns it. This exists ONLY for uploads that must succeed-or-fail together with
/// their owner row in one DB transaction — e.g. an applicant cannot be created without its
/// CNIC/membership-card scans, and a <c>documents</c> row cannot exist before its owner row does
/// (see the <c>ck_documents_exactly_one_owner</c> check). Everything else keeps using the
/// multipart <c>POST /api/documents</c> endpoint. It composes <c>IDocumentService</c>, it does not
/// duplicate storage or validation logic.
/// </summary>
public record InlineFileUpload(string FileName, string ContentType, string ContentBase64)
{
    /// <summary>
    /// The same 10 MB ceiling <c>DocumentService</c> enforces on the multipart upload path —
    /// shared here so it isn't hand-copied a third time between the two validators.
    /// </summary>
    public const long MaxSizeBytes = 10 * 1024 * 1024;
}
