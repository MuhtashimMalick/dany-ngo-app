namespace NgoFund.Contracts.Applications;

/// <summary>One entry in a <see cref="ReplaceApplicationGuarantorsRequest"/>. <see cref="Id"/> is
/// null for a new guarantor being added, and set (echoing the value from a prior
/// <c>GET</c>/<c>PUT .../guarantors</c> response) for an existing guarantor being edited — matching
/// Ids are updated in place rather than replaced, so a guarantor's uploaded documents (which hang
/// off <c>application_guarantor_id</c>) stay reachable across edits.</summary>
public record GuarantorEntry(
    Guid? Id,
    int SequenceNumber,
    string? MembershipNumber,
    string FullName,
    string? FatherName,
    string? GrandfatherName,
    string? Surname,
    string? Cnic,
    string? ResidentialAddress,
    string? BusinessAddress,
    string? BusinessNature,
    string? PhoneHome,
    string? PhoneOffice,
    string? PhoneMobile,
    DateTimeOffset? DeclarationAcceptedAt);

public record ReplaceApplicationGuarantorsRequest(IReadOnlyList<GuarantorEntry> Guarantors);
