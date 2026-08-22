namespace NgoFund.Domain.Entities;

/// <summary>
/// Category-specific fields for a HOUSE_RENT application, re-keyed from the Housing Google Form.
/// Shares its primary key with <see cref="FundApplication"/> (true 1:1) rather than having its own
/// UUID — there is never more than one of these per application.
/// </summary>
public class HousingApplicationDetails
{
    public Guid ApplicationId { get; set; }
    public FundApplication Application { get; set; } = null!;

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
