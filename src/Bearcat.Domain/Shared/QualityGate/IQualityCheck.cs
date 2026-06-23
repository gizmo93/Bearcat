using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Shared.QualityGate;

public interface IQualityCheck
{
    QualityCheckRuleType RuleType { get; }

    IReadOnlyList<QualityCheckParameterDescriptor> Parameters { get; }

    IReadOnlyList<string> Evaluate(QualityCheckRule rule, QualityCheckContext context);
}
