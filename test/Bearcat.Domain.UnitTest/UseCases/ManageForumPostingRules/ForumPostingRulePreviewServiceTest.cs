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
        var results = await service.PreviewAsync(
            distributionSiteRegistrationId: 3,
            entityType: ForumPostTemplateType.Release
        );

        // Assert
        var result = results.ShouldHaveSingleItem();
        result.EntityId.ShouldBe(7);
        result.EntityName.ShouldBe("Movie.1080p");
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
        var results = await service.PreviewAsync(
            distributionSiteRegistrationId: 3,
            entityType: ForumPostTemplateType.Release
        );

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
        var results = await service.PreviewAsync(
            distributionSiteRegistrationId: 3,
            entityType: ForumPostTemplateType.Release
        );

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
        var results = await service.PreviewAsync(
            distributionSiteRegistrationId: 3,
            entityType: ForumPostTemplateType.Release
        );

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
        await service.PreviewAsync(
            distributionSiteRegistrationId: 42,
            entityType: ForumPostTemplateType.Release,
            entityCount: 12
        );

        // Assert
        repository.RequestedDistributionSiteRegistrationId.ShouldBe(42);
        repository.RequestedEntityCount.ShouldBe(12);
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
        var results = await service.PreviewAsync(
            distributionSiteRegistrationId: 3,
            entityType: ForumPostTemplateType.Release
        );

        // Assert
        results.Select(result => result.EntityId).ShouldBe([3, 2, 1]);
    }

    [Test]
    public async Task PreviewAsync_ReleaseCollection_MatchesCollectionRuleAgainstMemberClassifications()
    {
        // Arrange
        var repository = new FakeForumPostingRuleReadRepository
        {
            Rules =
            [
                RuleWith(id: 1, sortOrder: 0, resolutions: ["R1080p"]),
                RuleWith(
                    id: 2,
                    sortOrder: 1,
                    resolutions: ["R1080p"],
                    templateType: ForumPostTemplateType.ReleaseCollection
                ),
            ],
            ReleaseCollections =
            [
                ReleaseCollectionWith(
                    id: 5,
                    name: "Show.S01.1080p",
                    ReleaseWith(id: 11, name: "Show.S01E01.1080p", ReleaseResolution.R1080p),
                    ReleaseWith(id: 12, name: "Show.S01E02.1080p", ReleaseResolution.R1080p)
                ),
            ],
        };
        var service = new ForumPostingRulePreviewService(repository);

        // Act
        var results = await service.PreviewAsync(
            distributionSiteRegistrationId: 3,
            entityType: ForumPostTemplateType.ReleaseCollection,
            entityCount: 8
        );

        // Assert
        var result = results.ShouldHaveSingleItem();
        result.EntityId.ShouldBe(5);
        result.EntityName.ShouldBe("Show.S01.1080p");
        result.MatchedRuleName.ShouldBe("Rule 2");
        repository.RequestedEntityCount.ShouldBe(8);
    }

    [Test]
    public async Task PreviewAsync_ReleaseCollectionWithOnlyReleaseRules_ReportsCollectionWithoutMatch()
    {
        // Arrange
        var repository = new FakeForumPostingRuleReadRepository
        {
            Rules = [RuleWith(id: 1, sortOrder: 0, resolutions: ["R1080p"])],
            Releases = [ReleaseWith(id: 7, name: "Movie.1080p", ReleaseResolution.R1080p)],
            ReleaseCollections =
            [
                ReleaseCollectionWith(
                    id: 5,
                    name: "Show.S01.1080p",
                    ReleaseWith(id: 11, name: "Show.S01E01.1080p", ReleaseResolution.R1080p)
                ),
            ],
        };
        var service = new ForumPostingRulePreviewService(repository);

        // Act
        var results = await service.PreviewAsync(
            distributionSiteRegistrationId: 3,
            entityType: ForumPostTemplateType.ReleaseCollection
        );

        // Assert
        var result = results.ShouldHaveSingleItem();
        result.EntityId.ShouldBe(5);
        result.MatchedRuleName.ShouldBeNull();
    }

    private static ForumPostingRule RuleWith(
        int id,
        int sortOrder,
        string[] resolutions,
        bool isEnabled = true,
        ForumPostTemplateType templateType = ForumPostTemplateType.Release
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
            ForumPostTemplate = new ForumPostTemplate { Id = 1, Type = templateType },
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

    private static ReleaseCollection ReleaseCollectionWith(
        int id,
        string name,
        params Release[] releases
    )
    {
        return new ReleaseCollection
        {
            Id = id,
            Name = name,
            ReleaseGroup = new ReleaseGroup { Name = "Group" },
            Releases = [.. releases],
        };
    }

    private sealed class FakeForumPostingRuleReadRepository : IForumPostingRuleReadRepository
    {
        public IReadOnlyList<ForumPostingRule> Rules { get; init; } = [];

        public IReadOnlyList<Release> Releases { get; init; } = [];

        public IReadOnlyList<ReleaseCollection> ReleaseCollections { get; init; } = [];

        public int? RequestedDistributionSiteRegistrationId { get; private set; }

        public int? RequestedEntityCount { get; private set; }

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
            RequestedEntityCount = count;

            return Task.FromResult(Releases);
        }

        public Task<IReadOnlyList<ReleaseCollection>> GetRecentReleaseCollectionsForPreviewAsync(
            int count,
            CancellationToken cancellationToken = default
        )
        {
            RequestedEntityCount = count;

            return Task.FromResult(ReleaseCollections);
        }
    }
}
