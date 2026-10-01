using System.Security.Cryptography;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Documents;
using NgoFund.Domain.Exceptions;

namespace NgoFund.Infrastructure.Services;

/// <summary>Result of a validated-and-stored upload, ready to assign onto a <see cref="Domain.Entities.Document"/> row.</summary>
internal readonly record struct StoredFile(string StorageKey, long SizeBytes, string Sha256);

/// <summary>
/// The upload validation + storage pipeline shared by <see cref="DocumentService.UploadAsync"/> and
/// <see cref="ApplicantService.ReplaceProfileDocumentAsync"/>: content-type allowlist, size ceiling,
/// magic-byte sniffing, SHA-256 hashing, then <see cref="IFileStorage.SaveAsync"/> — one place, so
/// neither caller can drift out of sync on what's allowed.
/// </summary>
internal static class DocumentFileValidator
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "application/pdf",
    };

    internal const long MaxSizeBytes = InlineFileUpload.MaxSizeBytes;

    internal static async Task<StoredFile> ValidateAndStoreAsync(
        IFileStorage fileStorage, Stream content, string contentType, CancellationToken cancellationToken)
    {
        if (!AllowedContentTypes.Contains(contentType))
        {
            throw new InvalidFileException($"Files of type '{contentType}' are not supported. Allowed: JPEG, PNG, WebP, PDF.");
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

        var bytes = buffer.ToArray();
        if (!MatchesDeclaredContentType(bytes, contentType))
        {
            throw new InvalidFileException($"The uploaded file's contents do not match the declared type '{contentType}'.");
        }

        var sha256 = Convert.ToHexString(SHA256.HashData(bytes));

        buffer.Position = 0;
        var storageKey = await fileStorage.SaveAsync(buffer, cancellationToken);

        return new StoredFile(storageKey, buffer.Length, sha256);
    }

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
