using NgoFund.Domain.Common;

namespace NgoFund.Domain.Entities;

/// <summary>Org-wide configuration: name, logo, currency, receipt header/footer, fiscal year start.</summary>
public class AppSetting : BaseEntity, IUpdateAuditable
{
    /// <summary>Natural key used as the lookup key, e.g. "OrgName", "Currency". Unique.</summary>
    public string Key { get; set; } = null!;

    public string? Value { get; set; }

    /// <summary>Hint for how the API/UI should render/parse <see cref="Value"/> (string, number, bool, date, json).</summary>
    public string DataType { get; set; } = "string";

    public string? Description { get; set; }

    public bool IsEditable { get; set; } = true;

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
