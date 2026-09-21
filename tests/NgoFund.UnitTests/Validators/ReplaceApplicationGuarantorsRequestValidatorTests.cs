using NgoFund.Application.Validators;
using NgoFund.Contracts.Applications;

namespace NgoFund.UnitTests.Validators;

/// <summary>
/// Item 7 (2026-09 feedback round 3): a guarantor must be reachable — all five contact fields
/// (PhoneMobile, PhoneHome, PhoneOffice, ResidentialAddress, BusinessAddress) are required, not
/// just PhoneMobile (already covered by an integration test elsewhere). One FluentValidation unit
/// test per field is faster than a full HTTP round trip for this.
/// </summary>
public class ReplaceApplicationGuarantorsRequestValidatorTests
{
    private readonly ReplaceApplicationGuarantorsRequestValidator _validator = new();

    private static GuarantorEntry ValidGuarantor() => new(
        Id: null, SequenceNumber: 1, MembershipNumber: "MEM-1", FullName: "Guarantor One",
        FatherName: null, GrandfatherName: null, Surname: null, Cnic: "11111-1111111-1",
        ResidentialAddress: "123 Test Street", BusinessAddress: "456 Business Road", BusinessNature: null,
        PhoneHome: "021-1111111", PhoneOffice: "021-1111112", PhoneMobile: "0300-1111111",
        DeclarationAcceptedAt: null);

    [Fact]
    public void FullyValidGuarantor_Passes()
    {
        var result = _validator.Validate(new ReplaceApplicationGuarantorsRequest([ValidGuarantor()]));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void MissingPhoneHome_Fails()
    {
        var guarantor = ValidGuarantor() with { PhoneHome = null };
        var result = _validator.Validate(new ReplaceApplicationGuarantorsRequest([guarantor]));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains(nameof(GuarantorEntry.PhoneHome)));
    }

    [Fact]
    public void MissingPhoneOffice_Fails()
    {
        var guarantor = ValidGuarantor() with { PhoneOffice = null };
        var result = _validator.Validate(new ReplaceApplicationGuarantorsRequest([guarantor]));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains(nameof(GuarantorEntry.PhoneOffice)));
    }

    [Fact]
    public void MissingResidentialAddress_Fails()
    {
        var guarantor = ValidGuarantor() with { ResidentialAddress = null };
        var result = _validator.Validate(new ReplaceApplicationGuarantorsRequest([guarantor]));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains(nameof(GuarantorEntry.ResidentialAddress)));
    }

    [Fact]
    public void MissingBusinessAddress_Fails()
    {
        var guarantor = ValidGuarantor() with { BusinessAddress = null };
        var result = _validator.Validate(new ReplaceApplicationGuarantorsRequest([guarantor]));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains(nameof(GuarantorEntry.BusinessAddress)));
    }
}
