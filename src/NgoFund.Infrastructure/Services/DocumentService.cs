using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Documents;
using NgoFund.Domain.Applications;
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

    private const long MaxSizeBytes = InlineFileUpload.MaxSizeBytes;

    public async Task<DocumentDto> UploadAsync(
        Stream content, string fileName, string contentType, string documentType,
        Guid? applicantId, Guid? applicationId, Guid? donationId, Guid? paymentId, Guid? applicationGuarantorId,
        string? slotKey, string? description,
        CancellationToken cancellationToken)
    {
        if (!AllowedContentTypes.Contains(contentType))
        {
            throw new InvalidFileException($"Files of type '{contentType}' are not supported. Allowed: JPEG, PNG, WebP, PDF.");
        }

        var ownerCount = new[] { applicantId, applicationId, donationId, paymentId, applicationGuarantorId }.Count(x => x is not null);
        if (ownerCount != 1)
        {
            throw new InvalidFileException("A document must be attached to exactly one owner.");
        }

        if (!Enum.TryParse<DocumentType>(documentType, out var parsedDocumentType))
        {
            throw new InvalidFileException($"'{documentType}' is not a recognized document type.");
        }

        if (slotKey is not null)
        {
            await EnsureSlotMatchesAsync(slotKey, parsedDocumentType, applicantId, applicationId, applicationGuarantorId, cancellationToken);
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

        if (!MatchesDeclaredContentType(buffer.ToArray(), contentType))
        {
            throw new InvalidFileException($"The uploaded file's contents do not match the declared type '{contentType}'.");
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
            ApplicationGuarantorId = applicationGuarantorId,
            SlotKey = slotKey,
            Description = description,
            UploadedAt = DateTimeOffset.UtcNow,
        };

        dbContext.Documents.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(document);
    }

    /// <summary>Validates a client-supplied <paramref name="slotKey"/> against the target
    /// application/guarantor's category manifest before the upload is accepted — the slot must
    /// exist for that category, the document type must be one it accepts, and the owner arm
    /// (application vs. guarantor) must match the slot's scope.
    ///
    /// v1.4 (B6): an applicant-owned upload can never carry a slotKey, and is rejected outright
    /// rather than validated. Unlike an application or a guarantor, an applicant has no single
    /// category to resolve a manifest against — an applicant can have applications across several
    /// categories (or none yet), so there is no category-specific slot list to check a client-
    /// supplied slotKey against. The alternative (deriving "a" category from, say, the applicant's
    /// most recent application) would be arbitrary and could silently validate against the wrong
    /// category's manifest. Applicant profile documents legitimately carry no slot key — see
    /// ApplicationCompletenessEvaluator.EvaluateSlots, which matches them by DocumentType alone —
    /// so rejecting a client-supplied one here is the coherent choice, not a gap.</summary>
    private async Task EnsureSlotMatchesAsync(
        string slotKey, DocumentType documentType, Guid? applicantId, Guid? applicationId, Guid? applicationGuarantorId, CancellationToken cancellationToken)
    {
        string categoryCode;
        DocumentSlotOwnerScope ownerScope;

        if (applicationId is not null)
        {
            categoryCode = await dbContext.Applications
                .Where(a => a.Id == applicationId)
                .Select(a => a.ApplicationCategory.Code)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new EntityNotFoundException("Application", applicationId.Value);
            ownerScope = DocumentSlotOwnerScope.Application;
        }
        else if (applicationGuarantorId is not null)
        {
            categoryCode = await dbContext.ApplicationGuarantors
                .Where(g => g.Id == applicationGuarantorId)
                .Select(g => g.Application.ApplicationCategory.Code)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new EntityNotFoundException("ApplicationGuarantor", applicationGuarantorId.Value);
            ownerScope = DocumentSlotOwnerScope.Guarantor;
        }
        else if (applicantId is not null)
        {
            throw new DocumentSlotMismatchException($"slotKey '{slotKey}' was supplied on an applicant-owned upload; applicant profile documents cannot carry a slot key.");
        }
        else
        {
            throw new DocumentSlotMismatchException($"slotKey '{slotKey}' was supplied, but this upload isn't attached to an application or a guarantor.");
        }

        var slot = ApplicationRequirements.FindSlot(categoryCode, slotKey)
            ?? throw new DocumentSlotMismatchException($"'{slotKey}' is not a recognized document slot for category '{categoryCode}'.");

        if (!slot.AcceptedTypes.Contains(documentType))
        {
            throw new DocumentSlotMismatchException($"Document type '{documentType}' is not accepted for slot '{slotKey}'.");
        }

        if (slot.OwnerScope != ownerScope)
        {
            throw new DocumentSlotMismatchException($"Slot '{slotKey}' is {slot.OwnerScope}-scoped, not {ownerScope}-scoped.");
        }
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

    public async Task<IReadOnlyList<DocumentDto>> GetForGuarantorAsync(Guid guarantorId, CancellationToken cancellationToken)
    {
        var docs = await dbContext.Documents.AsNoTracking()
            .Where(d => d.ApplicationGuarantorId == guarantorId)
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

    private static DocumentDto Map(Document d) => new(d.Id, d.FileName, d.ContentType, d.SizeBytes, d.DocumentType.ToString(), d.Description, d.UploadedAt, d.SlotKey);

    /// <summary>
    /// Sniffs the leading bytes and checks them against the declared content type, so an upload
    /// can't lie about what it is (: uploads are validated by content, not extension/
    /// declared type). Deliberately a small private helper, not a NuGet package — the BCL is
    /// enough for four fixed magic-byte signatures.
    /// </summary>
    private static bool MatchesDeclaredContentType(byte[] bytes, string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        "image/png" => bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(stackalloc byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        "image/webp" => bytes.Length >= 12
            && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8)
            && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        "application/pdf" => bytes.Length >= 4 && bytes.AsSpan(0, 4).SequenceEqual("%PDF"u8),
        _ => false,
    };
}
