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
            applicantId, applicationId, donationId: null, paymentId: null, CancellationToken.None);

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
            applicantId: Guid.NewGuid(), applicationId: null, donationId: null, paymentId: null, CancellationToken.None));
    }
}
