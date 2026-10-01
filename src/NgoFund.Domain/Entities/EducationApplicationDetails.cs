namespace NgoFund.Domain.Entities;

/// <summary>
/// Category-specific fields for an EDUCATION application, re-keyed from the Education Google Form.
/// Shares its primary key with <see cref="FundApplication"/> (true 1:1), same pattern as
/// <see cref="HousingApplicationDetails"/>. The applicant record on an EDUCATION application is the
/// student's father/guardian — student and mother data live here; father data lives on <see cref="Applicant"/>.
/// </summary>
public class EducationApplicationDetails
{
    public Guid ApplicationId { get; set; }
    public FundApplication Application { get; set; } = null!;

    public string? CensusNumber { get; set; }

    public string StudentName { get; set; } = null!;

    public string? WmoId { get; set; }

    public string? StudentMobile { get; set; }

    public string CurrentClass { get; set; } = null!;

    public string? PreviousClass { get; set; }

    /// <summary>The form labels this "Roll Number / Total Marks (last exam)" — stored as free text, not numeric.</summary>
    public string? LastExamTotalMarks { get; set; }

    public string? LastExamMarksObtained { get; set; }

    public decimal? PreviousYearAttendancePercent { get; set; }

    public int? TotalAttendanceDays { get; set; }

    public int? TotalAcademicDays { get; set; }

    public string? FatherJamaat { get; set; }

    public string MotherName { get; set; } = null!;

    public string? MotherFatherName { get; set; }

    public string? MotherCaste { get; set; }

    public string? MotherJamaat { get; set; }

    public string? MotherMembershipNumber { get; set; }

    public string? MotherCnic { get; set; }

    public decimal? MotherMonthlyIncome { get; set; }

    public string? MotherMobile { get; set; }

    public string? MotherProfession { get; set; }
}
