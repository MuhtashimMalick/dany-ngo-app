namespace NgoFund.Contracts.Documents;

public record DocumentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    string DocumentType,
    string? Description,
    DateTimeOffset UploadedAt,
    string? SlotKey);
