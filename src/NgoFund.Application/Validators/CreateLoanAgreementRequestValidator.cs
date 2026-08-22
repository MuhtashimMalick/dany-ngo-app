using FluentValidation;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Loans;
using NgoFund.Domain.Enums;

namespace NgoFund.Application.Validators;

public class CreateLoanAgreementRequestValidator : AbstractValidator<CreateLoanAgreementRequest>
{
    public CreateLoanAgreementRequestValidator(IAppSettingService appSettingService)
    {
        RuleFor(x => x.ApplicationId).NotEmpty();

        RuleFor(x => x.InstallmentCount).GreaterThanOrEqualTo(1);

        // Upper bound comes from app_settings ("loan.max_installment_count") rather than a
        // hardcoded constant, so an org can tune it without a code change.
        RuleFor(x => x.InstallmentCount)
            .MustAsync(async (count, cancellationToken) =>
            {
                var settings = await appSettingService.GetAllAsync(cancellationToken);
                var maxSetting = settings.SingleOrDefault(s => s.Key == "loan.max_installment_count");
                var max = maxSetting?.Value is { } value && int.TryParse(value, out var parsed) ? parsed : 60;
                return count <= max;
            })
            .WithMessage("InstallmentCount exceeds the configured maximum (loan.max_installment_count).")
            .When(x => x.InstallmentCount >= 1);

        RuleFor(x => x.FirstDueDate)
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("FirstDueDate cannot be in the past.");

        RuleFor(x => x.Frequency).NotEmpty().Must(v => Enum.TryParse<LoanInstallmentFrequency>(v, out _))
            .WithMessage($"Frequency must be one of: {string.Join(", ", Enum.GetNames<LoanInstallmentFrequency>())}.");

        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}
