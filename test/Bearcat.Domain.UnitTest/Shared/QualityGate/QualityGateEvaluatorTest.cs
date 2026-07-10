using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.QualityGate;
using Bearcat.Domain.Shared.QualityGate.Checks;
using Bearcat.Domain.ValueObjects;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared.QualityGate;

public class QualityGateEvaluatorTest
{
    private static readonly DateTime EvaluatedAt = new(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Evaluate_NoProfile_ReturnsPassed()
    {
        // Arrange
        var evaluator = CreateEvaluator();

        // Act
        var result = evaluator.Evaluate(CreateRelease(), profile: null);

        // Assert
        result.State.ShouldBe(QualityGateState.Passed);
        result.Issues.ShouldBeEmpty();
    }

    [Test]
    public void Evaluate_ProfileWithoutRules_ReturnsPassed()
    {
        // Arrange
        var evaluator = CreateEvaluator();
        var profile = new QualityProfile { Name = "Empty", Rules = [] };

        // Act
        var result = evaluator.Evaluate(CreateRelease(), profile);

        // Assert
        result.State.ShouldBe(QualityGateState.Passed);
        result.Issues.ShouldBeEmpty();
    }

    [Test]
    public void Evaluate_FailingRule_ReturnsFailedWithMappedIssue()
    {
        // Arrange
        var evaluator = CreateEvaluator();
        var profile = CreateProfile(RequireNfoRule());

        // Act
        var result = evaluator.Evaluate(CreateRelease(), profile);

        // Assert
        result.State.ShouldBe(QualityGateState.Failed);
        result.Issues.ShouldHaveSingleItem();
        result.Issues[0].RuleType.ShouldBe(QualityCheckRuleType.RequiredReleaseInfo);
        result.Issues[0].Description.ShouldBe("NFO is missing");
    }

    [Test]
    public void Evaluate_MultipleFailingRules_AggregatesAllIssues()
    {
        // Arrange
        var evaluator = CreateEvaluator(new FakeFileSystemService { TotalBytes = 0 });
        var profile = CreateProfile(RequireNfoRule(), MinimumFolderSizeRule(100));

        // Act
        var result = evaluator.Evaluate(CreateRelease(), profile);

        // Assert
        result.State.ShouldBe(QualityGateState.Failed);
        result.Issues.Count.ShouldBe(2);
        result
            .Issues.Select(i => i.RuleType)
            .ShouldContain(QualityCheckRuleType.RequiredReleaseInfo);
        result.Issues.Select(i => i.RuleType).ShouldContain(QualityCheckRuleType.MinimumFolderSize);
    }

    [Test]
    public void Evaluate_RuleWithoutRegisteredCheck_IsSkipped()
    {
        // Arrange
        var evaluator = new QualityGateEvaluator([], new FakeFileSystemService());
        var profile = CreateProfile(RequireNfoRule());

        // Act
        var result = evaluator.Evaluate(CreateRelease(), profile);

        // Assert
        result.State.ShouldBe(QualityGateState.Passed);
        result.Issues.ShouldBeEmpty();
    }

    [Test]
    public void EvaluateAndApply_FailingRelease_SetsFailedStateAndStoresIssues()
    {
        // Arrange
        var evaluator = CreateEvaluator();
        var release = CreateRelease();
        release.ReleaseGroup.QualityProfile = CreateProfile(RequireNfoRule());

        // Act
        evaluator.EvaluateAndApply(release, EvaluatedAt);

        // Assert
        release.QualityGateState.ShouldBe(QualityGateState.Failed);
        release.QualityGateEvaluatedAt.ShouldBe(EvaluatedAt);
        release.QualityIssues.ShouldHaveSingleItem();
        release.QualityIssues[0].RuleType.ShouldBe(QualityCheckRuleType.RequiredReleaseInfo);
        release.QualityIssues[0].Description.ShouldBe("NFO is missing");
    }

    [Test]
    public void EvaluateAndApply_PassingRelease_ClearsPreviousIssues()
    {
        // Arrange
        var evaluator = CreateEvaluator();
        var release = CreateRelease();
        release.ReleaseGroup.QualityProfile = CreateProfile(RequireNfoRule());
        release.QualityGateState = QualityGateState.Failed;
        release.QualityIssues =
        [
            new ReleaseQualityIssue
            {
                RuleType = QualityCheckRuleType.RequiredReleaseInfo,
                Description = "NFO is missing",
            },
        ];
        release.ReleaseInfo = new ReleaseInfo
        {
            NfoDatabaseClassName = ReleaseInfo.ManualSource,
            ReleaseName = release.Name,
        };
        release.ReleaseNfo = new ReleaseNfo { FileName = "release.nfo", Content = "NFO body" };

        // Act
        evaluator.EvaluateAndApply(release, EvaluatedAt);

        // Assert
        release.QualityGateState.ShouldBe(QualityGateState.Passed);
        release.QualityIssues.ShouldBeEmpty();
    }

    [Test]
    public void EvaluateAndApply_ManuallyApprovedRelease_LeavesStateUntouched()
    {
        // Arrange
        var evaluator = CreateEvaluator();
        var release = CreateRelease();
        release.ReleaseGroup.QualityProfile = CreateProfile(RequireNfoRule());
        release.QualityGateState = QualityGateState.ManuallyApproved;

        // Act
        evaluator.EvaluateAndApply(release, EvaluatedAt);

        // Assert
        release.QualityGateState.ShouldBe(QualityGateState.ManuallyApproved);
        release.QualityGateEvaluatedAt.ShouldBeNull();
        release.QualityIssues.ShouldBeEmpty();
    }

    [Test]
    public void EvaluateAndApply_UnmanagedRelease_IsNotEvaluated()
    {
        // Arrange
        var evaluator = CreateEvaluator();
        var release = CreateRelease();
        release.ReleaseType = ReleaseType.Unmanaged;
        release.ReleaseGroup.QualityProfile = CreateProfile(RequireNfoRule());

        // Act
        evaluator.EvaluateAndApply(release, EvaluatedAt);

        // Assert
        release.QualityGateState.ShouldBe(QualityGateState.NotEvaluated);
        release.QualityGateEvaluatedAt.ShouldBeNull();
        release.QualityIssues.ShouldBeEmpty();
    }

    private static QualityGateEvaluator CreateEvaluator(
        FakeFileSystemService? fileSystemService = null
    ) =>
        new(
            [
                new FilePatternQualityCheck(),
                new MinimumFolderSizeQualityCheck(),
                new RequiredReleaseInfoQualityCheck(),
                new MediaInfoQualityCheck(),
            ],
            fileSystemService ?? new FakeFileSystemService()
        );

    private static QualityProfile CreateProfile(params QualityCheckRule[] rules) =>
        new() { Name = "Profile", Rules = [.. rules] };

    private static QualityCheckRule RequireNfoRule() =>
        new()
        {
            RuleType = QualityCheckRuleType.RequiredReleaseInfo,
            ParametersJson = QualityCheckParameterValues.Serialize(
                new Dictionary<string, object?>
                {
                    ["requireCover"] = false,
                    ["requireDescription"] = false,
                    ["requireNfo"] = true,
                }
            ),
        };

    private static QualityCheckRule MinimumFolderSizeRule(int minimumMegabytes) =>
        new()
        {
            RuleType = QualityCheckRuleType.MinimumFolderSize,
            ParametersJson = QualityCheckParameterValues.Serialize(
                new Dictionary<string, object?> { ["minimumMegabytes"] = minimumMegabytes }
            ),
        };

    private static Release CreateRelease() =>
        new()
        {
            Name = "Bearcat.Release.001",
            ReleaseFolderPath = "/release",
            ReleaseGroup = new ReleaseGroup { Name = "Group" },
        };
}
