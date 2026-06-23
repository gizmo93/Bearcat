using System.IO.Enumeration;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Shared.QualityGate.Checks;

public sealed class FilePatternQualityCheck : IQualityCheck
{
    private const string PatternKey = "pattern";

    public QualityCheckRuleType RuleType => QualityCheckRuleType.FilePatternPresent;

    public IReadOnlyList<QualityCheckParameterDescriptor> Parameters =>
        [
            new(
                PatternKey,
                QualityCheckParameterKind.Text,
                "*.nfo",
                LabelKey: "FilePattern",
                HelperTextKey: "FilePatternHelp",
                Placeholder: "*.nfo"
            ),
        ];

    public IReadOnlyList<string> Evaluate(QualityCheckRule rule, QualityCheckContext context)
    {
        var pattern = QualityCheckParameterValues.Parse(rule.ParametersJson).GetString(PatternKey);

        var hasMatch = context.Files.Any(file =>
            FileSystemName.MatchesSimpleExpression(
                pattern,
                Path.GetFileName(file),
                ignoreCase: true
            )
        );

        return hasMatch
            ? []
            : [$"No file matching pattern '{pattern}' found in the release folder"];
    }
}
