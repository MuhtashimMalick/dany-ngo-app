using NgoFund.Contracts.Documents;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Shared, real file content for integration tests — DocumentService content-sniffs uploads (A4),
/// so placeholder strings like "fake image bytes" no longer round-trip as a declared image type.
/// One shared constant per format, reused everywhere, rather than a base64 blob copy-pasted at
/// every call site.
/// </summary>
internal static class TestFiles
{
    // A real, valid 1x1 transparent PNG (67 bytes).
    private const string MinimalPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

    public static readonly byte[] MinimalPngBytes = Convert.FromBase64String(MinimalPngBase64);

    /// <summary>image/jpeg-declared bytes that pass DocumentService's magic-byte check
    /// (FF D8 FF) — not a fully decodable JPEG, since nothing in this codebase renders uploaded
    /// images, only stores and serves back the original bytes.</summary>
    public static readonly byte[] MinimalJpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0xFF, 0xD9];

    /// <summary>The one applicant-creation upload used for the required CnicFront, CnicBack, and
    /// MembershipCard slots (see A3's CreateApplicantRequestValidator) across every test that
    /// creates an applicant through the API.</summary>
    public static readonly InlineFileUpload MinimalPngUpload = new("test.png", "image/png", MinimalPngBase64);
}
