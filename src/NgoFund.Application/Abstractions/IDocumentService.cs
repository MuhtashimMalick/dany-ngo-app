using NgoFund.Contracts.Documents;

namespace NgoFund.Application.Abstractions;

public interface IDocumentService
{
    Task<DocumentDto> UploadAsync(
        Stream content, string fileName, string contentType, string documentType,
        Guid? applicantId, Guid? applicationId, Guid? donationId, Guid? paymentId,
        CancellationToken cancellationToken);

    Task<(Stream Content, DocumentDto Metadata)> DownloadAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentDto>> GetForApplicantAsync(Guid applicantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentDto>> GetForApplicationAsync(Guid applicationId, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
