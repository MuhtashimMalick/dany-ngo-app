using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Documents;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;
using NgoFund.Domain.Exceptions;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.Infrastructure.Services;

public class DocumentService(AppDbContext dbContext, IFileStorage fileStorage) : IDocumentService
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "application/pdf",
    };

    private const long MaxSizeBytes = 10 * 1024 * 1024;

    public async Task<DocumentDto> UploadAsync(
        Stream content, string fileName, string contentType, string documentType,
        Guid? applicantId, Guid? applicationId, Guid? donationId, Guid? paymentId,
        CancellationToken cancellationToken)
    {
        if (!AllowedContentTypes.Contains(contentType))
        {
            throw new InvalidFileException($"Files of type '{contentType}' are not supported. Allowed: JPEG, PNG, WebP, PDF.");
        }

        var ownerCount = new[] { applicantId, applicationId, donationId, paymentId }.Count(x => x is not null);
        if (ownerCount != 1)
        {
            throw new InvalidFileException("A document must be attached to exactly one owner.");
        }

        if (!Enum.TryParse<DocumentType>(documentType, out var parsedDocumentType))
        {
            throw new InvalidFileException($"'{documentType}' is not a recognized document type.");
        }

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        if (buffer.Length == 0)
        {
            throw new InvalidFileException("The uploaded file is empty.");
        }

        if (buffer.Length > MaxSizeBytes)
        {
            throw new InvalidFileException($"File exceeds the maximum allowed size of {MaxSizeBytes / 1024 / 1024} MB.");
        }

        var sha256 = Convert.ToHexString(SHA256.HashData(buffer.ToArray()));

        buffer.Position = 0;
        var storageKey = await fileStorage.SaveAsync(buffer, cancellationToken);

        var document = new Document
        {
            FileName = fileName,
            StorageKey = storageKey,
            ContentType = contentType,
            SizeBytes = buffer.Length,
            Sha256 = sha256,
            DocumentType = parsedDocumentType,
            ApplicantId = applicantId,
            ApplicationId = applicationId,
            DonationId = donationId,
            PaymentId = paymentId,
            UploadedAt = DateTimeOffset.UtcNow,
        };

        dbContext.Documents.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(document);
    }

    public async Task<(Stream Content, DocumentDto Metadata)> DownloadAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await dbContext.Documents.FindAsync([id], cancellationToken)
            ?? throw new EntityNotFoundException("Document", id);

        var stream = await fileStorage.OpenReadAsync(document.StorageKey, cancellationToken);
        return (stream, Map(document));
    }

    public async Task<IReadOnlyList<DocumentDto>> GetForApplicantAsync(Guid applicantId, CancellationToken cancellationToken)
    {
        var docs = await dbContext.Documents.AsNoTracking()
            .Where(d => d.ApplicantId == applicantId)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(cancellationToken);

        return docs.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<DocumentDto>> GetForApplicationAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var docs = await dbContext.Documents.AsNoTracking()
            .Where(d => d.ApplicationId == applicationId)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(cancellationToken);

        return docs.Select(Map).ToList();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await dbContext.Documents.FindAsync([id], cancellationToken)
            ?? throw new EntityNotFoundException("Document", id);

        await fileStorage.DeleteAsync(document.StorageKey, cancellationToken);

        dbContext.Documents.Remove(document);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static DocumentDto Map(Document d) => new(d.Id, d.FileName, d.ContentType, d.SizeBytes, d.DocumentType.ToString(), d.Description, d.UploadedAt);
}
