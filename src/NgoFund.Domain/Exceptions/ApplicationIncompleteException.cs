namespace NgoFund.Domain.Exceptions;

/// <summary>
/// Raised at the Approved-transition call site when <c>ApplicationCompletenessEvaluator.Evaluate</c>
/// finds required fields or required documents still missing — the fix for the client's bug report:
/// applications were reaching Approved with zero category-specific details and zero documents on
/// file. Mirrors <see cref="GuarantorsRequiredException"/>'s shape/call site.
/// </summary>
public sealed class ApplicationIncompleteException(
    string applicationNumber, IReadOnlyList<string> missingFieldLabels, IReadOnlyList<string> missingDocumentLabels)
    : DomainException(BuildMessage(applicationNumber, missingFieldLabels, missingDocumentLabels)), IProblemDetailExtensions
{
    public IReadOnlyList<string> MissingFieldLabels { get; } = missingFieldLabels;
    public IReadOnlyList<string> MissingDocumentLabels { get; } = missingDocumentLabels;

    public IReadOnlyDictionary<string, object?> GetProblemDetailExtensions() => new Dictionary<string, object?>
    {
        ["missingFields"] = MissingFieldLabels,
        ["missingDocuments"] = MissingDocumentLabels,
    };

    private static string BuildMessage(string applicationNumber, IReadOnlyList<string> missingFieldLabels, IReadOnlyList<string> missingDocumentLabels)
    {
        var parts = new List<string>();
        if (missingFieldLabels.Count > 0)
        {
            parts.Add($"missing fields: {string.Join(", ", missingFieldLabels)}");
        }

        if (missingDocumentLabels.Count > 0)
        {
            parts.Add($"missing documents: {string.Join(", ", missingDocumentLabels)}");
        }

        return $"Application {applicationNumber} cannot be Approved — {string.Join("; ", parts)}.";
    }
}
