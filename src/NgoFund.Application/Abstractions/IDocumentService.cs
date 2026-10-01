using NgoFund.Contracts.Documents;

namespace NgoFund.Application.Abstractions;

public interface IDocumentService
{
    /// <summary>When <paramref name="slotKey"/> is supplied, it's validated against the target
    /// application/guarantor's category manifest (<see cref="Domain.Applications.ApplicationRequirements"/>)
    /// before the upload is accepted — see <see cref="Domain.Exceptions.DocumentSlotMismatchException"/>.</summary>
    /// <summary><paramref name="externalFileReference"/> (A6) is the Google Drive file id, set
    /// only by the Google Form intake pipeline — content-sniffing validation stays the single
    /// enforcement point for every caller, this just tags the resulting row for upload idempotency.</summary>
    Task<DocumentDto> UploadAsync(
        Stream content, string fileName, string contentType, string documentType,
        Guid? applicantId, Guid? applicationId, Guid? donationId, Guid? paymentId, Guid? applicationGuarantorId,
        string? slotKey, string? description,
        CancellationToken cancellationToken, string? externalFileReference = null);

    Task<(Stream Content, DocumentDto Metadata)> DownloadAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentDto>> GetForApplicantAsync(Guid applicantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentDto>> GetForApplicationAsync(Guid applicationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentDto>> GetForGuarantorAsync(Guid guarantorId, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
