using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Shared.QualityGate.Checks;

public sealed class MediaInfoQualityCheck : IQualityCheck
{
    public QualityCheckRuleType RuleType => QualityCheckRuleType.MediaInfoPresent;

    public IReadOnlyList<QualityCheckParameterDescriptor> Parameters => [];

    public IReadOnlyList<string> Evaluate(QualityCheckRule rule, QualityCheckContext context)
    {
        return context.Release.MediaFiles.Count > 0 ? [] : ["No media info has been extracted"];
    }
}
