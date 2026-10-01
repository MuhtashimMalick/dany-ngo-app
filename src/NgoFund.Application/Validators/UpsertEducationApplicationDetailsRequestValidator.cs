using FluentValidation;
using NgoFund.Contracts.Applications;
using NgoFund.Contracts.Common;

namespace NgoFund.Application.Validators;

public class UpsertEducationApplicationDetailsRequestValidator : AbstractValidator<UpsertEducationApplicationDetailsRequest>
{
    public UpsertEducationApplicationDetailsRequestValidator()
    {
        RuleFor(x => x.StudentName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CurrentClass).NotEmpty().MaximumLength(50);
        RuleFor(x => x.MotherName).NotEmpty().MaximumLength(200);

        RuleFor(x => x.CensusNumber).MaximumLength(50);
        RuleFor(x => x.WmoId).MaximumLength(50);
        RuleFor(x => x.PreviousClass).MaximumLength(50);
        RuleFor(x => x.LastExamTotalMarks).MaximumLength(50);
        RuleFor(x => x.LastExamMarksObtained).MaximumLength(50);
        RuleFor(x => x.FatherJamaat).MaximumLength(100);
        RuleFor(x => x.MotherFatherName).MaximumLength(200);
        RuleFor(x => x.MotherCaste).MaximumLength(100);
        RuleFor(x => x.MotherJamaat).MaximumLength(100);
        RuleFor(x => x.MotherMembershipNumber).MaximumLength(PakistaniFormats.JamaatMembershipMaxLength);
        RuleFor(x => x.MotherProfession).MaximumLength(100);

        RuleFor(x => x.StudentMobile).MaximumLength(12).Matches(PakistaniFormats.MobilePhonePattern)
            .WithMessage(PakistaniFormats.MobilePhoneMessage)
            .When(x => !string.IsNullOrEmpty(x.StudentMobile));
        RuleFor(x => x.MotherMobile).MaximumLength(12).Matches(PakistaniFormats.MobilePhonePattern)
            .WithMessage(PakistaniFormats.MobilePhoneMessage)
            .When(x => !string.IsNullOrEmpty(x.MotherMobile));
        RuleFor(x => x.MotherCnic).MaximumLength(15).Matches(PakistaniFormats.CnicPattern)
            .WithMessage(PakistaniFormats.CnicMessage)
            .When(x => !string.IsNullOrEmpty(x.MotherCnic));

        RuleFor(x => x.PreviousYearAttendancePercent).InclusiveBetween(0, 100).When(x => x.PreviousYearAttendancePercent is not null);
        RuleFor(x => x.TotalAttendanceDays).GreaterThanOrEqualTo(0).When(x => x.TotalAttendanceDays is not null);
        RuleFor(x => x.TotalAcademicDays).GreaterThan(0).When(x => x.TotalAcademicDays is not null);
        RuleFor(x => x.MotherMonthlyIncome).GreaterThanOrEqualTo(0).When(x => x.MotherMonthlyIncome is not null);

        // Mirrors ck_education_details_attendance_le_academic_days.
        RuleFor(x => x)
            .Must(x => x.TotalAttendanceDays is null || x.TotalAcademicDays is null || x.TotalAttendanceDays <= x.TotalAcademicDays)
            .WithMessage("TotalAttendanceDays cannot exceed TotalAcademicDays.")
            .WithName("TotalAttendanceDays");
    }
}
