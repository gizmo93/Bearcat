using Bearcat.Abstractions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Shared.QualityGate;

public sealed record QualityIssueResult(QualityCheckRuleType RuleType, string Description);

public sealed record QualityGateEvaluationResult(
    QualityGateState State,
    IReadOnlyList<QualityIssueResult> Issues
);

public sealed class QualityGateEvaluator(
    IEnumerable<IQualityCheck> checks,
    IFileSystemService fileSystemService
)
{
    private readonly IReadOnlyList<IQualityCheck> checks = checks.ToList();

    public QualityGateEvaluationResult Evaluate(Release release, QualityProfile? profile)
    {
        if (profile is null || profile.Rules.Count == 0)
        {
            return new QualityGateEvaluationResult(QualityGateState.Passed, []);
        }

        var context = new QualityCheckContext(release, fileSystemService);
        var issues = new List<QualityIssueResult>();

        foreach (var rule in profile.Rules)
        {
            var check = checks.FirstOrDefault(c => c.RuleType == rule.RuleType);

            if (check is null)
            {
                continue;
            }

            foreach (var description in check.Evaluate(rule, context))
            {
                issues.Add(new QualityIssueResult(rule.RuleType, description));
            }
        }

        var state = issues.Count == 0 ? QualityGateState.Passed : QualityGateState.Failed;

        return new QualityGateEvaluationResult(state, issues);
    }

    public void EvaluateAndApply(Release release, DateTime evaluatedAt)
    {
        if (release.ReleaseType is ReleaseType.Unmanaged)
        {
            return;
        }

        if (release.QualityGateState == QualityGateState.ManuallyApproved)
        {
            return;
        }

        var result = Evaluate(release, release.ReleaseGroup.QualityProfile);

        release.QualityGateState = result.State;
        release.QualityGateEvaluatedAt = evaluatedAt;
        release.QualityIssues.Clear();

        foreach (var issue in result.Issues)
        {
            release.QualityIssues.Add(
                new ReleaseQualityIssue
                {
                    RuleType = issue.RuleType,
                    Description = issue.Description,
                }
            );
        }
    }
}
