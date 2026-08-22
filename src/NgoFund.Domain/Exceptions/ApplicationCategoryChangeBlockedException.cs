namespace NgoFund.Domain.Exceptions;

/// <summary>
/// Raised when an update tries to change <c>FundApplication.ApplicationCategoryId</c> after
/// category-scoped data already exists for the application: its current category's details row
/// (housing/marriage/business-loan), any <c>ApplicationGuarantor</c> rows, or any <c>Document</c>
/// saved into a slot. That data is scoped to the ORIGINAL category — reassigning the category out
/// from under it would orphan it behind <c>ApplicationDetailsService.EnsureCategoryAsync</c>'s
/// mismatch guard rather than migrating or deleting it, so the change is rejected outright.
/// </summary>
public sealed class ApplicationCategoryChangeBlockedException(string applicationNumber, string currentCategoryName)
    : DomainException($"Application {applicationNumber}'s category cannot be changed from '{currentCategoryName}': category-specific data already exists for it.");
