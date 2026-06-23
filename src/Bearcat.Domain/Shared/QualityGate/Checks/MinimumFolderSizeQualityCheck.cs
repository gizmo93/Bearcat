using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Shared.QualityGate.Checks;

public sealed class MinimumFolderSizeQualityCheck : IQualityCheck
{
    private const long BytesPerMegabyte = 1024 * 1024;
    private const string MinimumMegabytesKey = "minimumMegabytes";

    public QualityCheckRuleType RuleType => QualityCheckRuleType.MinimumFolderSize;

    public IReadOnlyList<QualityCheckParameterDescriptor> Parameters =>
        [
            new(
                MinimumMegabytesKey,
                QualityCheckParameterKind.Integer,
                100,
                LabelKey: "MinimumFolderSizeMb",
                Minimum: 0
            ),
        ];

    public IReadOnlyList<string> Evaluate(QualityCheckRule rule, QualityCheckContext context)
    {
        var minimumMegabytes = QualityCheckParameterValues
            .Parse(rule.ParametersJson)
            .GetInt(MinimumMegabytesKey);

        var minimumBytes = minimumMegabytes * BytesPerMegabyte;

        return context.TotalBytes >= minimumBytes
            ? []
            : [$"Release folder is smaller than the required {minimumMegabytes} MB"];
    }
}
