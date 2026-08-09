using FluentValidation;
using NgoFund.Contracts.FundCategories;

namespace NgoFund.Application.Validators;

public class CreateFundCategoryRequestValidator : AbstractValidator<CreateFundCategoryRequest>
{
    public CreateFundCategoryRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
