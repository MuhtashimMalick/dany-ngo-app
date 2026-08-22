using NgoFund.Domain.Applications;

namespace NgoFund.UnitTests.Domain;

public class BusinessLoanCapitalCalculatorTests
{
    [Fact]
    public void ComputeAmountMismatchWarning_ExactMatch_ReturnsNull()
    {
        var warning = BusinessLoanCapitalCalculator.ComputeAmountMismatchWarning(
            requestedAmount: 50000m, capitalRequired: 80000m, capitalAlreadyAvailable: 30000m);

        Assert.Null(warning);
    }

    [Fact]
    public void ComputeAmountMismatchWarning_WithinTolerance_ReturnsNull()
    {
        // Gap = 51000, requested = 50000 -> within the 5% tolerance.
        var warning = BusinessLoanCapitalCalculator.ComputeAmountMismatchWarning(
            requestedAmount: 50000m, capitalRequired: 81000m, capitalAlreadyAvailable: 30000m);

        Assert.Null(warning);
    }

    [Fact]
    public void ComputeAmountMismatchWarning_LargeMismatch_ReturnsWarning()
    {
        var warning = BusinessLoanCapitalCalculator.ComputeAmountMismatchWarning(
            requestedAmount: 50000m, capitalRequired: 200000m, capitalAlreadyAvailable: 30000m);

        Assert.NotNull(warning);
        Assert.Contains("does not roughly match", warning);
    }

    [Fact]
    public void ComputeAmountMismatchWarning_CapitalRequiredMissing_ReturnsNull()
    {
        Assert.Null(BusinessLoanCapitalCalculator.ComputeAmountMismatchWarning(50000m, null, 30000m));
    }

    [Fact]
    public void ComputeAmountMismatchWarning_CapitalAlreadyAvailableMissing_ReturnsNull()
    {
        Assert.Null(BusinessLoanCapitalCalculator.ComputeAmountMismatchWarning(50000m, 80000m, null));
    }

    [Fact]
    public void ComputeAmountMismatchWarning_BothCapitalFiguresMissing_ReturnsNull()
    {
        Assert.Null(BusinessLoanCapitalCalculator.ComputeAmountMismatchWarning(50000m, null, null));
    }
}
