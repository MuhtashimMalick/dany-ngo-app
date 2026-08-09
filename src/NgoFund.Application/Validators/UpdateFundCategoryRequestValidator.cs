using FluentValidation;
using NgoFund.Contracts.FundCategories;

namespace NgoFund.Application.Validators;

public class UpdateFundCategoryRequestValidator : AbstractValidator<UpdateFundCategoryRequest>
{
    public UpdateFundCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
