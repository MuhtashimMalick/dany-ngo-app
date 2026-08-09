using NgoFund.Domain.Common;

namespace NgoFund.Domain.Entities;

/// <summary>
/// A free-text remark on an application. <see cref="IsInternal"/> = true marks it as an internal
/// admin note, hidden from lower-privilege roles (e.g. Viewer).
/// </summary>
public class ApplicationRemark : BaseEntity
{
    public Guid ApplicationId { get; set; }
    public FundApplication Application { get; set; } = null!;

    public string Remark { get; set; } = null!;

    public bool IsInternal { get; set; }
}
