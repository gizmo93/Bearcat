using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class QualityCheckRule
{
    public int Id { get; set; }

    public int QualityProfileId { get; set; }

    public QualityProfile QualityProfile { get; set; } = null!;

    public QualityCheckRuleType RuleType { get; set; }

    public string ParametersJson { get; set; } = null!;
}
