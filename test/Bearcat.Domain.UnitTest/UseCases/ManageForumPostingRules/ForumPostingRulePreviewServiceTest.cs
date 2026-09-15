using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ForumPostingRules;
using Bearcat.Domain.UseCases.ManageForumPostingRules;
using Bearcat.Domain.UseCases.ManageForumPostingRules.ReadModels;
using Bearcat.Domain.UseCases.ManageForumPostingRules.Repositories;
using Bearcat.Domain.ValueObjects;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageForumPostingRules;

public class ForumPostingRulePreviewServiceTest
{
    [Test]
    public async Task PreviewAsync_MatchingRule_ReportsRuleNameAndTarget()
    {
        // Arrange
        var repository = new FakeForumPostingRuleReadRepository
        {
            Rules = [RuleWith(id: 1, sortOrder: 0, resolutions: ["R1080p"])],
            Releases = [ReleaseWith(id: 7, name: "Movie.1080p", ReleaseResolution.R1080p)],
        };
        var service = new ForumPostingRulePreviewService(repository);

        // Act
        var results = await service.PreviewAsync(distributionSiteRegistrationId: 3);

        // Assert
        var result = results.ShouldHaveSingleItem();
        result.ReleaseId.ShouldBe(7);
        result.ReleaseName.ShouldBe("Movie.1080p");
        result.MatchedRuleName.ShouldBe("Rule 1");
        result.TargetPathSnapshot.ShouldBe("Board › Movies › HD");
    }

    [Test]
    public async Task PreviewAsync_NoMatchingRule_ReportsReleaseWithoutMatch()
    {
        // Arrange
        var repository = new FakeForumPostingRuleReadRepository
        {
            Rules = [RuleWith(id: 1, sortOrder: 0, resolutions: ["R2160p"])],
            Releases = [ReleaseWith(id: 8, name: "Movie.1080p", ReleaseResolution.R1080p)],
        };
        var service = new ForumPostingRulePreviewService(repository);

        // Act
        var results = await service.PreviewAsync(distributionSiteRegistrationId: 3);

        // Assert
        var result = results.ShouldHaveSingleItem();
        result.MatchedRuleName.ShouldBeNull();
        result.TargetPathSnapshot.ShouldBeNull();
    }

    [Test]
    public async Task PreviewAsync_SeveralMatchingRules_UsesLowestSortOrder()
    {
        // Arrange
        var repository = new FakeForumPostingRuleReadRepository
        {
            Rules =
            [
                RuleWith(id: 1, sortOrder: 2, resolutions: ["R1080p"]),
                RuleWith(id: 2, sortOrder: 1, resolutions: ["R1080p", "R720p"]),
            ],
            Releases = [ReleaseWith(id: 9, name: "Movie.1080p", ReleaseResolution.R1080p)],
        };
        var service = new ForumPostingRulePreviewService(repository);

        // Act
        var results = await service.PreviewAsync(distributionSiteRegistrationId: 3);

        // Assert
        results.ShouldHaveSingleItem().MatchedRuleName.ShouldBe("Rule 2");
    }

    [Test]
    public async Task PreviewAsync_DisabledRule_IsIgnored()
    {
        // Arrange
        var repository = new FakeForumPostingRuleReadRepository
        {
            Rules = [RuleWith(id: 1, sortOrder: 0, resolutions: ["R1080p"], isEnabled: false)],
            Releases = [ReleaseWith(id: 10, name: "Movie.1080p", ReleaseResolution.R1080p)],
        };
        var service = new ForumPostingRulePreviewService(repository);

        // Act
        var results = await service.PreviewAsync(distributionSiteRegistrationId: 3);

        // Assert
        results.ShouldHaveSingleItem().MatchedRuleName.ShouldBeNull();
    }

    [Test]
    public async Task PreviewAsync_PassesRegistrationAndCountToRepository()
    {
        // Arrange
        var repository = new FakeForumPostingRuleReadRepository();
        var service = new ForumPostingRulePreviewService(repository);

        // Act
        await service.PreviewAsync(distributionSiteRegistrationId: 42, releaseCount: 12);

        // Assert
        repository.RequestedDistributionSiteRegistrationId.ShouldBe(42);
        repository.RequestedReleaseCount.ShouldBe(12);
    }

    [Test]
    public async Task PreviewAsync_KeepsRepositoryOrderOfReleases()
    {
        // Arrange
        var repository = new FakeForumPostingRuleReadRepository
        {
            Rules = [],
            Releases =
            [
                ReleaseWith(id: 3, name: "Third", ReleaseResolution.R1080p),
                ReleaseWith(id: 2, name: "Second", ReleaseResolution.R1080p),
                ReleaseWith(id: 1, name: "First", ReleaseResolution.R1080p),
            ],
        };
        var service = new ForumPostingRulePreviewService(repository);

        // Act
        var results = await service.PreviewAsync(distributionSiteRegistrationId: 3);

        // Assert
        results.Select(result => result.ReleaseId).ShouldBe([3, 2, 1]);
    }

    private static ForumPostingRule RuleWith(
        int id,
        int sortOrder,
        string[] resolutions,
        bool isEnabled = true
    )
    {
        return new ForumPostingRule
        {
            Id = id,
            SortOrder = sortOrder,
            Name = $"Rule {id}",
            ConditionJson = RuleConditionSerializer.Serialize(
                RuleCondition.CompareMany(
                    RuleFieldCatalog.Resolution,
                    RuleConditionOperator.In,
                    resolutions
                )
            ),
            TargetNodeId = "42",
            TargetPathSnapshot = "Board › Movies › HD",
            ForumPostTemplateId = 1,
            PostMode = ForumPostPostMode.AlwaysNewThread,
            IsEnabled = isEnabled,
        };
    }

    private static Release ReleaseWith(int id, string name, ReleaseResolution resolution)
    {
        return new Release
        {
            Id = id,
            Name = name,
            Classification = new ReleaseClassification { Resolution = resolution },
        };
    }

    private sealed class FakeForumPostingRuleReadRepository : IForumPostingRuleReadRepository
    {
        public IReadOnlyList<ForumPostingRule> Rules { get; init; } = [];

        public IReadOnlyList<Release> Releases { get; init; } = [];

        public int? RequestedDistributionSiteRegistrationId { get; private set; }

        public int? RequestedReleaseCount { get; private set; }

        public Task<IReadOnlyList<ForumPostingRuleSummaryReadModel>> GetAllAsync(
            int distributionSiteRegistrationId,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotSupportedException();
        }

        public Task<ForumPostingRuleDetailReadModel?> GetDetailAsync(
            int forumPostingRuleId,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ForumPostingRule>> GetRulesForMatchingAsync(
            int distributionSiteRegistrationId,
            CancellationToken cancellationToken = default
        )
        {
            RequestedDistributionSiteRegistrationId = distributionSiteRegistrationId;

            return Task.FromResult(Rules);
        }

        public Task<IReadOnlyList<Release>> GetRecentReleasesForPreviewAsync(
            int count,
            CancellationToken cancellationToken = default
        )
        {
            RequestedReleaseCount = count;

            return Task.FromResult(Releases);
        }
    }
}
