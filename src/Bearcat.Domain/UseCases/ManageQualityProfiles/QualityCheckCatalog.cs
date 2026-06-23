using Bearcat.Domain.Shared.QualityGate;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageQualityProfiles;

public class QualityCheckCatalog(IEnumerable<IQualityCheck> checks)
{
    private readonly IReadOnlyList<IQualityCheck> checks = checks.ToList();

    public IReadOnlyList<QualityCheckRuleType> RuleTypes =>
        checks.Select(check => check.RuleType).ToList();

    public IReadOnlyList<QualityCheckParameterDescriptor> GetParameters(
        QualityCheckRuleType ruleType
    ) => checks.FirstOrDefault(check => check.RuleType == ruleType)?.Parameters ?? [];
}
