using FluentValidation;
using NgoFund.Contracts.ApplicationCategories;

namespace NgoFund.Application.Validators;

public class CreateApplicationCategoryRequestValidator : AbstractValidator<CreateApplicationCategoryRequest>
{
    public CreateApplicationCategoryRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DefaultMaxAmount).GreaterThan(0).When(x => x.DefaultMaxAmount is not null);
    }
}
