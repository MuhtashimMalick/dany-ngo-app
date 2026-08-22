using NgoFund.Contracts.Documents;

namespace NgoFund.Application.Abstractions;

public interface IDocumentService
{
    /// <summary>When <paramref name="slotKey"/> is supplied, it's validated against the target
    /// application/guarantor's category manifest (<see cref="Domain.Applications.ApplicationRequirements"/>)
    /// before the upload is accepted — see <see cref="Domain.Exceptions.DocumentSlotMismatchException"/>.</summary>
    Task<DocumentDto> UploadAsync(
        Stream content, string fileName, string contentType, string documentType,
        Guid? applicantId, Guid? applicationId, Guid? donationId, Guid? paymentId, Guid? applicationGuarantorId,
        string? slotKey, string? description,
        CancellationToken cancellationToken);

    Task<(Stream Content, DocumentDto Metadata)> DownloadAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentDto>> GetForApplicantAsync(Guid applicantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentDto>> GetForApplicationAsync(Guid applicationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentDto>> GetForGuarantorAsync(Guid guarantorId, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
