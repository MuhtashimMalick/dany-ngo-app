namespace NgoFund.Contracts.Applications;

public record UpsertHousingApplicationDetailsRequest(
    int? ApplicantAge,
    decimal? CurrentHouseValue,
    decimal? MonthlyRent,
    decimal? AdvancePaid,
    int? YearsAtCurrentAddress,
    string? PreviousResidentialAddress,
    bool ReceivedAssistanceBefore,
    string? PreviousAssistanceDetails,
    bool ReceivesMarriageAssistance,
    bool ReceivesEducationAssistance,
    bool ReceivesMedicalAssistance,
    bool ReceivesWidowAssistance);
