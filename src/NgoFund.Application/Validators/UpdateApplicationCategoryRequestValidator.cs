using FluentValidation;
using NgoFund.Contracts.ApplicationCategories;

namespace NgoFund.Application.Validators;

public class UpdateApplicationCategoryRequestValidator : AbstractValidator<UpdateApplicationCategoryRequest>
{
    public UpdateApplicationCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DefaultMaxAmount).GreaterThan(0).When(x => x.DefaultMaxAmount is not null);
    }
}
