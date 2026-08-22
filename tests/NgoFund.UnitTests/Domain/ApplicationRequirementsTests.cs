using NgoFund.Domain.Applications;
using NgoFund.Domain.Enums;

namespace NgoFund.UnitTests.Domain;

/// <summary>
/// Protects the completeness manifest's shape: slot keys are persisted strings
/// (<c>documents.slot_key</c>) — renaming or dropping one silently orphans existing uploads, so
/// the exact set is pinned as a golden list rather than only checked for "some reasonable count".
/// </summary>
public class ApplicationRequirementsTests
{
    [Theory]
    [InlineData("HOUSE_RENT")]
    [InlineData("SHAADI")]
    [InlineData("ROZGAR")]
    public void DocumentSlotsFor_SlotKeysAreUniqueWithinCategory(string categoryCode)
    {
        var slots = ApplicationRequirements.DocumentSlotsFor(categoryCode);

        Assert.Equal(slots.Select(s => s.SlotKey).Distinct().Count(), slots.Count);
    }

    [Theory]
    [InlineData("HOUSE_RENT")]
    [InlineData("SHAADI")]
    [InlineData("ROZGAR")]
    public void DocumentSlotsFor_EveryRequiredSlot_HasAtLeastOneAcceptedTypeAndPositiveMinCount(string categoryCode)
    {
        foreach (var slot in ApplicationRequirements.DocumentSlotsFor(categoryCode).Where(s => s.IsRequired))
        {
            Assert.NotEmpty(slot.AcceptedTypes);
            Assert.True(slot.MinCount >= 1, $"{slot.SlotKey} has MinCount {slot.MinCount}");
        }
    }

    [Theory]
    [InlineData("HEALTH")]
    [InlineData("EDUCATION")]
    [InlineData("EMERGENCY")]
    [InlineData("OTHER")]
    public void DocumentSlotsFor_NonFormCategories_ReturnsEmpty(string categoryCode)
    {
        Assert.Empty(ApplicationRequirements.DocumentSlotsFor(categoryCode));
    }

    [Theory]
    [InlineData("HEALTH")]
    [InlineData("EDUCATION")]
    [InlineData("EMERGENCY")]
    [InlineData("OTHER")]
    public void RequiredFieldsFor_NonFormCategories_ReturnsEmpty(string categoryCode)
    {
        Assert.Empty(ApplicationRequirements.RequiredFieldsFor(categoryCode));
    }

    [Fact]
    public void DocumentSlotsFor_HouseRent_MatchesGoldenSlotKeySet()
    {
        var keys = ApplicationRequirements.DocumentSlotsFor("HOUSE_RENT").Select(s => s.SlotKey).ToHashSet();

        Assert.Equal(
        [
            "HOUSE_RENT.APPLICANT_CNIC", "HOUSE_RENT.MEMBERSHIP_CARD", "HOUSE_RENT.UTILITY_BILLS", "HOUSE_RENT.RENT_RECEIPTS",
        ], keys);
    }

    [Fact]
    public void DocumentSlotsFor_Shaadi_MatchesGoldenSlotKeySet()
    {
        var keys = ApplicationRequirements.DocumentSlotsFor("SHAADI").Select(s => s.SlotKey).ToHashSet();

        Assert.Equal(
        [
            "SHAADI.APPLICANT_CNIC", "SHAADI.APPLICANT_MEMBERSHIP_CARD", "SHAADI.WEDDING_CARD",
            "SHAADI.BRIDE_CNIC_OR_BFORM", "SHAADI.GROOM_CNIC", "SHAADI.BRIDE_MEMBERSHIP_CARD",
        ], keys);
    }

    [Fact]
    public void DocumentSlotsFor_Rozgar_MatchesGoldenSlotKeySet()
    {
        var keys = ApplicationRequirements.DocumentSlotsFor("ROZGAR").Select(s => s.SlotKey).ToHashSet();

        Assert.Equal(
        [
            "ROZGAR.LOAN_APPLICATION", "ROZGAR.BUSINESS_DETAILS", "ROZGAR.APPLICANT_CNIC", "ROZGAR.FORM_B",
            "ROZGAR.MEMBERSHIP_CARD", "ROZGAR.PASSPORT_PHOTOS", "ROZGAR.UTILITY_BILLS",
            "ROZGAR.GUARANTOR_CNIC", "ROZGAR.GUARANTOR_MEMBERSHIP_CARD",
        ], keys);
    }

    [Fact]
    public void DocumentSlotsFor_WeddingCardAndRentReceipts_AreOptional()
    {
        Assert.False(ApplicationRequirements.FindSlot("SHAADI", "SHAADI.WEDDING_CARD")!.IsRequired);
        Assert.False(ApplicationRequirements.FindSlot("HOUSE_RENT", "HOUSE_RENT.RENT_RECEIPTS")!.IsRequired);
    }

    [Fact]
    public void DocumentSlotsFor_RozgarGuarantorSlots_AreGuarantorScoped()
    {
        var guarantorSlots = ApplicationRequirements.DocumentSlotsFor("ROZGAR")
            .Where(s => s.OwnerScope == DocumentSlotOwnerScope.Guarantor)
            .Select(s => s.SlotKey)
            .ToHashSet();

        Assert.Equal(["ROZGAR.GUARANTOR_CNIC", "ROZGAR.GUARANTOR_MEMBERSHIP_CARD"], guarantorSlots);
    }

    [Fact]
    public void FindSlot_UnknownKey_ReturnsNull()
    {
        Assert.Null(ApplicationRequirements.FindSlot("ROZGAR", "ROZGAR.NOT_A_REAL_SLOT"));
    }
}
