using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ForumPostingRuleRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private ForumPostingRuleRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        var readDbContext = CreateDbContext();
        readDbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        repository = new ForumPostingRuleRepository(readDbContext, CreateDbContext());
    }

    [Test]
    public async Task GetRecentReleasesForPreviewAsync_ReleasesWithCloseTimestamps_ReturnsNewestReleasesByCreatedAtThenId()
    {
        // Arrange
        var firstRelease = AddRelease(
            "Bearcat.Release.001",
            new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc)
        );
        var secondRelease = AddRelease(
            "Bearcat.Release.002",
            new DateTime(2026, 9, 1, 10, 0, 0, 500, DateTimeKind.Utc)
        );
        var thirdRelease = AddRelease(
            "Bearcat.Release.003",
            new DateTime(2026, 9, 1, 10, 0, 0, 500, DateTimeKind.Utc)
        );
        AddRelease(
            "Bearcat.Release.004",
            new DateTime(2026, 8, 31, 23, 59, 59, 999, DateTimeKind.Utc)
        );
        var midnightRelease = AddRelease(
            "Bearcat.Release.005",
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
        );
        secondRelease.Classification = new ReleaseClassification
        {
            Title = "Bearcat Release",
            ContentType = ReleaseContentType.Movie,
            ClassifiedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetRecentReleasesForPreviewAsync(4, CancellationToken.None);

        // Assert
        result
            .Select(release => release.Id)
            .ShouldBe([thirdRelease.Id, secondRelease.Id, firstRelease.Id, midnightRelease.Id]);
        result[1].Classification.ShouldNotBeNull();
        result[1].Classification!.Title.ShouldBe("Bearcat Release");
        result[0].Classification.ShouldBeNull();
        result[0].ReleaseGroup.Name.ShouldBe("Bearcat.Release.003 group");
    }

    [Test]
    public async Task GetRecentReleaseCollectionsForPreviewAsync_SeveralCollections_ReturnsNewestCollectionsWithReleaseClassificationsAndGroup()
    {
        // Arrange
        var olderCollection = AddReleaseCollection(
            "Bearcat.Collection.001",
            new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc)
        );
        var newerCollection = AddReleaseCollection(
            "Bearcat.Collection.002",
            new DateTime(2026, 9, 1, 10, 0, 0, 500, DateTimeKind.Utc)
        );
        var tiedCollection = AddReleaseCollection(
            "Bearcat.Collection.003",
            new DateTime(2026, 9, 1, 10, 0, 0, 500, DateTimeKind.Utc)
        );
        AddReleaseCollection(
            "Bearcat.Collection.004",
            new DateTime(2026, 8, 31, 23, 59, 59, 999, DateTimeKind.Utc)
        );
        var classifiedRelease = AddRelease(
            "Bearcat.Release.E01",
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
        );
        classifiedRelease.Classification = new ReleaseClassification
        {
            Title = "Bearcat Show",
            ContentType = ReleaseContentType.TvShowEpisode,
            ClassifiedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var unclassifiedRelease = AddRelease(
            "Bearcat.Release.E02",
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
        );
        newerCollection.Releases = [classifiedRelease, unclassifiedRelease];
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetRecentReleaseCollectionsForPreviewAsync(
            3,
            CancellationToken.None
        );

        // Assert
        result
            .Select(collection => collection.Id)
            .ShouldBe([tiedCollection.Id, newerCollection.Id, olderCollection.Id]);
        result[1].ReleaseGroup.Name.ShouldBe("Bearcat.Collection.002 group");
        result[1].Releases.Count.ShouldBe(2);
        result[1]
            .Releases.Single(release => release.Id == classifiedRelease.Id)
            .Classification!.Title.ShouldBe("Bearcat Show");
        result[1]
            .Releases.Single(release => release.Id == unclassifiedRelease.Id)
            .Classification.ShouldBeNull();
        result[0].Releases.ShouldBeEmpty();
    }

    [Test]
    public async Task GetNextSortOrderAsync_RegistrationWithoutRules_ReturnsZero()
    {
        // Arrange
        var template = AddForumPostTemplate("Release post");
        var registration = AddRegistration("Forum A");
        var otherRegistration = AddRegistration("Forum B");
        AddRule(otherRegistration, template, "Other rule", sortOrder: 9);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetNextSortOrderAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0);
    }

    [Test]
    public async Task GetNextSortOrderAsync_RegistrationWithRules_ReturnsHighestSortOrderPlusOne()
    {
        // Arrange
        var template = AddForumPostTemplate("Release post");
        var registration = AddRegistration("Forum A");
        var otherRegistration = AddRegistration("Forum B");
        AddRule(registration, template, "Rule A", sortOrder: 0);
        AddRule(registration, template, "Rule B", sortOrder: 4);
        AddRule(registration, template, "Rule C", sortOrder: 2);
        AddRule(otherRegistration, template, "Other rule", sortOrder: 9);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetNextSortOrderAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(5);
    }

    private ForumPostTemplate AddForumPostTemplate(
        string name,
        ForumPostTemplateType type = ForumPostTemplateType.Release
    )
    {
        var template = new ForumPostTemplate
        {
            Name = name,
            Type = type,
            TemplateBody = string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        DbContext.ForumPostTemplates.Add(template);

        return template;
    }

    private DistributionSiteRegistration AddRegistration(string name)
    {
        var registration = new DistributionSiteRegistration
        {
            Name = name,
            DistributionSiteClassName = "TestForum",
            SerializedConfig = "{}",
            IsActive = true,
        };

        DbContext.DistributionSiteRegistrations.Add(registration);

        return registration;
    }

    private ForumPostingRule AddRule(
        DistributionSiteRegistration registration,
        ForumPostTemplate template,
        string name,
        int sortOrder,
        string conditionJson = "{}",
        string? threadPrefixId = null,
        ForumPostPostMode postMode = ForumPostPostMode.ReplyToExistingElseNewThread,
        bool isEnabled = true,
        DateTime? updatedAt = null
    )
    {
        var rule = new ForumPostingRule
        {
            DistributionSiteRegistration = registration,
            SortOrder = sortOrder,
            Name = name,
            ConditionJson = conditionJson,
            TargetNodeId = "12",
            TargetPathSnapshot = "Forum > Releases",
            ThreadPrefixId = threadPrefixId,
            ForumPostTemplate = template,
            PostMode = postMode,
            IsEnabled = isEnabled,
            CreatedAt = updatedAt ?? DateTime.UtcNow,
            UpdatedAt = updatedAt ?? DateTime.UtcNow,
        };

        DbContext.ForumPostingRules.Add(rule);

        return rule;
    }

    private Release AddRelease(string name, DateTime createdAt)
    {
        var release = new Release
        {
            Name = name,
            CreatedAt = createdAt,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/{name}",
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{name} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };

        DbContext.Releases.Add(release);

        return release;
    }

    private ReleaseCollection AddReleaseCollection(string name, DateTime createdAt)
    {
        var collection = new ReleaseCollection
        {
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{name} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
            Key = name.ToLowerInvariant(),
            Name = name,
            CreatedAt = createdAt,
        };

        DbContext.ReleaseCollections.Add(collection);

        return collection;
    }
}
