using System.ComponentModel.DataAnnotations;
using NgoFund.Contracts.Applications;

namespace NgoFund.Desktop.Components.Applications;

/// <summary>
/// Public, shared field models for the three category-detail extension tables (Housing/Marriage/
/// BusinessLoan) and for a single guarantor row. Promoted out of each section component's private
/// nested `FormModel` so the same model type — and the same DataAnnotations — can back both the
/// manage-modal "edit everything in one block" section components (HousingDetailsSection etc.) and
/// the new create-flow wizard's per-page Fields components (HousingDetailsFields etc.), without
/// duplicating the field list in two places. See ApplicationWizard.razor's doc comment for how the
/// two contexts share these.
/// </summary>
public class HousingDetailsFormModel
{
    public int? ApplicantAge { get; set; }
    public decimal? CurrentHouseValue { get; set; }
    public decimal? MonthlyRent { get; set; }
    public decimal? AdvancePaid { get; set; }
    public int? YearsAtCurrentAddress { get; set; }
    public string? PreviousResidentialAddress { get; set; }
    public bool ReceivedAssistanceBefore { get; set; }
    public string? PreviousAssistanceDetails { get; set; }
    public bool ReceivesMarriageAssistance { get; set; }
    public bool ReceivesEducationAssistance { get; set; }
    public bool ReceivesMedicalAssistance { get; set; }
    public bool ReceivesWidowAssistance { get; set; }
}

public class MarriageDetailsFormModel
{
    public string? GuardianRelationshipToBride { get; set; }

    [Required(ErrorMessage = "Bride name is required.")]
    public string BrideName { get; set; } = "";
    public string? BrideFatherName { get; set; }
    public string? BrideFamilyName { get; set; }
    public string? BrideCnic { get; set; }
    public string? BrideMaritalStatus { get; set; }
    public string? BridePreviousHusbandName { get; set; }
    public string? BrideJamaat { get; set; }
    public string? BridePriorTrustAssistance { get; set; }

    [Required(ErrorMessage = "Groom name is required.")]
    public string GroomName { get; set; } = "";
    public string? GroomFatherName { get; set; }
    public string? GroomGrandfatherName { get; set; }
    public string? GroomJamaat { get; set; }
    public string? GroomMaritalStatus { get; set; }
    public string? GroomPreviousWifeName { get; set; }
    public string? GroomAddress { get; set; }
    public string? GroomMobile { get; set; }
    public string? GroomBusinessAddress { get; set; }
    public DateOnly? NikahDate { get; set; }
    public DateOnly? RukhsatiDate { get; set; }
}

public class BusinessLoanDetailsFormModel
{
    public string? PaperFormNumber { get; set; }
    public string? BusinessPhone { get; set; }
    public string? Education { get; set; }
    public string? Skill { get; set; }
    public string? Experience { get; set; }
    public string? OtherIncomeSources { get; set; }
    public decimal? TotalMonthlyExpenses { get; set; }

    [Required(ErrorMessage = "Describe the proposed business.")]
    public string ProposedBusinessDescription { get; set; } = "";
    public string? ProposedBusinessLocation { get; set; }
    public decimal? CapitalRequired { get; set; }
    public decimal? CapitalAlreadyAvailable { get; set; }
    public bool HasPriorBusinessExperience { get; set; }
    public string? PriorBusinessDetails { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactCnic { get; set; }
    public string? EmergencyContactPhone { get; set; }
}

/// <summary>One ROZGAR guarantor row. Documents are deliberately not modeled here any more — per
/// the completeness milestone, guarantor documents are uploaded/displayed exclusively through the
/// Documents wizard page's guarantor-grouped <see cref="DocumentSlotList"/> (and the manage
/// modal's Documents tab, same component), never through a per-row upload control on this
/// editor.</summary>
public class GuarantorRowModel
{
    public Guid ClientKey { get; } = Guid.NewGuid();
    public Guid? Id { get; set; }
    public int SequenceNumber { get; set; }
    public string? MembershipNumber { get; set; }

    [Required(ErrorMessage = "Guarantor name is required.")]
    public string FullName { get; set; } = "";
    public string? FatherName { get; set; }
    public string? GrandfatherName { get; set; }
    public string? Surname { get; set; }
    public string? Cnic { get; set; }
    public string? ResidentialAddress { get; set; }
    public string? BusinessAddress { get; set; }
    public string? BusinessNature { get; set; }
    public string? PhoneHome { get; set; }
    public string? PhoneOffice { get; set; }
    public string? PhoneMobile { get; set; }
    public DateTimeOffset? DeclarationAcceptedAt { get; set; }

    public bool DeclarationAcceptedBool
    {
        get => DeclarationAcceptedAt is not null;
        set => DeclarationAcceptedAt = value ? DateTimeOffset.UtcNow : null;
    }

    /// <summary>Shared mapping from the server DTO — used after both the initial load and every
    /// save (GuarantorsEditor's manage-modal flow and ApplicationWizard's create-flow save point)
    /// so the two call sites can never drift into two different row shapes.</summary>
    public static GuarantorRowModel FromDto(ApplicationGuarantorDto g) => new()
    {
        Id = g.Id,
        SequenceNumber = g.SequenceNumber,
        MembershipNumber = g.MembershipNumber,
        FullName = g.FullName,
        FatherName = g.FatherName,
        GrandfatherName = g.GrandfatherName,
        Surname = g.Surname,
        Cnic = g.Cnic,
        ResidentialAddress = g.ResidentialAddress,
        BusinessAddress = g.BusinessAddress,
        BusinessNature = g.BusinessNature,
        PhoneHome = g.PhoneHome,
        PhoneOffice = g.PhoneOffice,
        PhoneMobile = g.PhoneMobile,
        DeclarationAcceptedAt = g.DeclarationAcceptedAt,
    };
}
