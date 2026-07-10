using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Shared.QualityGate.Checks;

public sealed class RequiredReleaseInfoQualityCheck : IQualityCheck
{
    private const string RequireCoverKey = "requireCover";
    private const string RequireDescriptionKey = "requireDescription";
    private const string RequireNfoKey = "requireNfo";

    public QualityCheckRuleType RuleType => QualityCheckRuleType.RequiredReleaseInfo;

    public IReadOnlyList<QualityCheckParameterDescriptor> Parameters =>
        [
            new(RequireCoverKey, QualityCheckParameterKind.Boolean, true, LabelKey: "RequireCover"),
            new(
                RequireDescriptionKey,
                QualityCheckParameterKind.Boolean,
                true,
                LabelKey: "RequireDescription"
            ),
            new(RequireNfoKey, QualityCheckParameterKind.Boolean, true, LabelKey: "RequireNfo"),
        ];

    public IReadOnlyList<string> Evaluate(QualityCheckRule rule, QualityCheckContext context)
    {
        var parameters = QualityCheckParameterValues.Parse(rule.ParametersJson);

        var release = context.Release;
        var metadata = release.Metadata;
        var issues = new List<string>();

        if (parameters.GetBool(RequireCoverKey) && string.IsNullOrWhiteSpace(metadata?.CoverUrl))
        {
            issues.Add("Cover image is missing");
        }

        if (
            parameters.GetBool(RequireDescriptionKey)
            && string.IsNullOrWhiteSpace(metadata?.Description)
        )
        {
            issues.Add("Description is missing");
        }

        if (
            parameters.GetBool(RequireNfoKey)
            && (release.ReleaseNfo is null || string.IsNullOrWhiteSpace(release.ReleaseNfo.Content))
        )
        {
            issues.Add("NFO is missing");
        }

        return issues;
    }
}
