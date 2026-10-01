using NgoFund.Contracts.Applicants;

namespace NgoFund.Desktop.Components.Applicants;

/// <summary>Which mode an <see cref="ApplicantDocumentSlot"/>/<see cref="ApplicantDocumentSlots"/>
/// renders in: <c>Create</c> stages a file client-side (parent owns the bytes, no applicant row
/// exists yet), <c>Edit</c> uploads immediately via <c>ApiClient.ReplaceApplicantDocumentAsync</c>,
/// <c>View</c> is read-only (view buttons only, no upload affordance).</summary>
public enum ApplicantDocumentMode
{
    Create,
    Edit,
    View,
}

/// <summary>Single source of truth for the three applicant profile-document categories' display
/// order and labels — was previously hardcoded three times (once per block) in Applicants.razor's
/// create modal. The profile photo (<see cref="NgoFund.Contracts.Applicants.ApplicantProfileDocumentTypes"/>
/// deliberately excludes it) is never part of this list; it keeps its own dedicated photo slot.</summary>
public static class ApplicantDocumentCategories
{
    public static readonly IReadOnlyList<(string DocumentType, string Label)> All = new[]
    {
        (ApplicantProfileDocumentTypes.CnicFront, "CNIC (Front)"),
        (ApplicantProfileDocumentTypes.CnicBack, "CNIC (Back)"),
        (ApplicantProfileDocumentTypes.MembershipCard, "Jamaat Membership Card"),
    };

    // Named accessors onto the same three entries above — used where a call site needs exactly one
    // category (e.g. the create modal's three standalone slots) without indexing into All by position.
    public static (string DocumentType, string Label) CnicFront => All[0];
    public static (string DocumentType, string Label) CnicBack => All[1];
    public static (string DocumentType, string Label) MembershipCard => All[2];
}
