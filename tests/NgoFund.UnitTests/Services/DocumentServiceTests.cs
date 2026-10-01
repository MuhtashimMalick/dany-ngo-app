using System.Reflection;
using Microsoft.EntityFrameworkCore;
using NgoFund.Application.Abstractions;
using NgoFund.Domain.Exceptions;
using NgoFund.Infrastructure.Persistence;
using NgoFund.Infrastructure.Services;
using NSubstitute;

namespace NgoFund.UnitTests.Services;

/// <summary>
/// The owner-count and document-type checks in <see cref="DocumentService.UploadAsync"/> must
/// throw before the database or file storage are touched, so a real Postgres connection is never
/// needed here — only the options need to type-check.
/// </summary>
public class DocumentServiceTests
{
    private static DocumentService CreateSut()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .Options;
        return new DocumentService(new AppDbContext(options), Substitute.For<IFileStorage>());
    }

    private static Task InvokeUploadAsync(DocumentService sut, Guid? applicantId, Guid? applicationId) =>
        sut.UploadAsync(
            new MemoryStream([1, 2, 3]), "photo.jpg", "image/jpeg", "ApplicantPhoto",
            applicantId, applicationId, donationId: null, paymentId: null, applicationGuarantorId: null,
            slotKey: null, description: null, CancellationToken.None);

    [Fact]
    public async Task UploadAsync_NoOwnerSpecified_ThrowsInvalidFileException()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidFileException>(() => InvokeUploadAsync(sut, applicantId: null, applicationId: null));
    }

    [Fact]
    public async Task UploadAsync_TwoOwnersSpecified_ThrowsInvalidFileException()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidFileException>(() => InvokeUploadAsync(sut, applicantId: Guid.NewGuid(), applicationId: Guid.NewGuid()));
    }

    [Fact]
    public async Task UploadAsync_UnrecognizedDocumentType_ThrowsInvalidFileException()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .Options;
        var sut = new DocumentService(new AppDbContext(options), Substitute.For<IFileStorage>());

        await Assert.ThrowsAsync<InvalidFileException>(() => sut.UploadAsync(
            new MemoryStream([1, 2, 3]), "photo.jpg", "image/jpeg", "NotARealDocumentType",
            applicantId: Guid.NewGuid(), applicationId: null, donationId: null, paymentId: null, applicationGuarantorId: null,
            slotKey: null, description: null, CancellationToken.None));
    }

    // --- A4: content-sniffing (magic bytes vs. declared content type) ---
    // MatchesDeclaredContentType is a private static helper on DocumentFileValidator (the shared
    // upload-validation pipeline both DocumentService and ApplicantService.ReplaceProfileDocumentAsync
    // go through); invoked via reflection rather than widening its visibility just for testability.

    private static readonly MethodInfo MatchesDeclaredContentTypeMethod = typeof(DocumentFileValidator)
        .GetMethod("MatchesDeclaredContentType", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static bool MatchesDeclaredContentType(byte[] bytes, string contentType) =>
        (bool)MatchesDeclaredContentTypeMethod.Invoke(null, [bytes, contentType])!;

    public static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
    public static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
    public static readonly byte[] WebpBytes = [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];
    public static readonly byte[] PdfBytes = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34];

    [Theory]
    [MemberData(nameof(AllowedTypeSamples))]
    public void MatchesDeclaredContentType_CorrectMagicBytes_ReturnsTrue(byte[] bytes, string contentType) =>
        Assert.True(MatchesDeclaredContentType(bytes, contentType));

    public static IEnumerable<object[]> AllowedTypeSamples()
    {
        yield return [JpegBytes, "image/jpeg"];
        yield return [PngBytes, "image/png"];
        yield return [WebpBytes, "image/webp"];
        yield return [PdfBytes, "application/pdf"];
    }

    [Fact]
    public void MatchesDeclaredContentType_PdfBytesDeclaredAsPng_ReturnsFalse() =>
        Assert.False(MatchesDeclaredContentType(PdfBytes, "image/png"));

    [Fact]
    public void MatchesDeclaredContentType_RandomBytes_ReturnsFalse() =>
        Assert.False(MatchesDeclaredContentType([0x01, 0x02, 0x03, 0x04, 0x05], "image/png"));

    [Fact]
    public async Task UploadAsync_ContentDoesNotMatchDeclaredType_ThrowsInvalidFileException()
    {
        var sut = CreateSut();

        // Declares PNG but the bytes are a PDF's magic number — must be rejected before ever
        // reaching file storage or the database.
        await Assert.ThrowsAsync<InvalidFileException>(() => sut.UploadAsync(
            new MemoryStream(PdfBytes), "not-a-png.png", "image/png", "ApplicantPhoto",
            applicantId: Guid.NewGuid(), applicationId: null, donationId: null, paymentId: null, applicationGuarantorId: null,
            slotKey: null, description: null, CancellationToken.None));
    }
}
