namespace NgoFund.Contracts.Applications;

public record UpsertEducationApplicationDetailsRequest(
    string? CensusNumber,
    string StudentName,
    string? WmoId,
    string? StudentMobile,
    string CurrentClass,
    string? PreviousClass,
    string? LastExamTotalMarks,
    string? LastExamMarksObtained,
    decimal? PreviousYearAttendancePercent,
    int? TotalAttendanceDays,
    int? TotalAcademicDays,
    string? FatherJamaat,
    string MotherName,
    string? MotherFatherName,
    string? MotherCaste,
    string? MotherJamaat,
    string? MotherMembershipNumber,
    string? MotherCnic,
    decimal? MotherMonthlyIncome,
    string? MotherMobile,
    string? MotherProfession);
