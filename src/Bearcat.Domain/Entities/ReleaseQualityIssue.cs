using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class ReleaseQualityIssue
{
    public int Id { get; set; }

    public int ReleaseId { get; set; }

    public Release Release { get; set; } = null!;

    public QualityCheckRuleType RuleType { get; set; }

    public string Description { get; set; } = null!;
}
