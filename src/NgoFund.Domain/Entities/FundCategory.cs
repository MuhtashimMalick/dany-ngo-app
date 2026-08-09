using NgoFund.Domain.Common;

namespace NgoFund.Domain.Entities;

/// <summary>
/// A fund pool donations are collected into and payments are drawn from (Zakat, General, ...).
/// Balances are never stored here — they are always derived from <see cref="FundTransaction"/>
/// via the <c>vw_fund_balances</c> view.
/// </summary>
public class FundCategory : BaseEntity, IUpdateAuditable, ISoftDeletable
{
    /// <summary>Short stable code, e.g. "ZAKAT", "GENERAL". Used in seed data and code, not just display.</summary>
    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>
    /// Whether this fund is subject to the Zakat rule: a non-Zakat-eligible application category
    /// may never be paid from a fund where <see cref="IsZakat"/> is true.
    /// </summary>
    public bool IsZakat { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
