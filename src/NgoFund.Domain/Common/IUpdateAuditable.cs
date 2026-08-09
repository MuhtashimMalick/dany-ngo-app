namespace NgoFund.Domain.Common;

/// <summary>Opt-in for entities that can be edited after creation.</summary>
public interface IUpdateAuditable
{
    DateTimeOffset? UpdatedAt { get; set; }
    Guid? UpdatedBy { get; set; }
}
