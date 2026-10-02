using Bearcat.Abstractions.DistributionSite;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Domain.Entities;
using Bearcat.Domain.IntegrationTest.Shared;
using Bearcat.Domain.UseCases.PostToForums.Models;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.PostToForums;

public class AutoForumPostingRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string ForumClassName = "TestForum";
    private const string BlogClassName = "TestBlog";

    private static readonly DateTime Now = new(2026, 9, 26, 12, 0, 0, 123, DateTimeKind.Utc);

    private AutoForumPostingRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        var readDbContext = CreateDbContext();
        readDbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        var distributionSiteFactoryMock = new Mock<IDistributionSiteFactory>(MockBehavior.Strict);
        distributionSiteFactoryMock
            .Setup(factory => factory.GetDistributionSites())
            .Returns([
                new DistributionSiteDto(
                    "Test forum",
                    ForumClassName,
                    DistributionSiteKind.Forum,
                    []
                ),
                new DistributionSiteDto("Test blog", BlogClassName, DistributionSiteKind.Blog, []),
            ]);

        repository = new AutoForumPostingRepository(
            readDbContext,
            CreateDbContext(),
            distributionSiteFactoryMock.Object,
            new ControllableTimeProvider(Now)
        );
    }

    [Test]
    public async Task GetForumRegistrationsWithEnabledRulesAsync_SeveralRegistrations_ReturnsActiveForumsWithEnabledRulesInSortOrder()
    {
        // Arrange
        var template = AddForumPostTemplate("Release post");
        var secondForum = AddRegistration("Forum B", enableAutomaticPosting: true);
        var secondForumLastRule = AddRule(secondForum, template, "Last", sortOrder: 2);
        AddRule(secondForum, template, "Disabled", sortOrder: 1, isEnabled: false);
        var secondForumFirstRule = AddRule(
            secondForum,
            template,
            "First",
            sortOrder: 0,
            conditionJson: "{\"kind\":\"Café\"}"
        );
        var secondForumTiedRule = AddRule(secondForum, template, "Tied", sortOrder: 0);
        var firstForum = AddRegistration("Forum A", stripDotsForThreadSearch: false);
        var firstForumRule = AddRule(firstForum, template, "Only", sortOrder: 0);
        var inactiveForum = AddRegistration("Forum C", isActive: false);
        AddRule(inactiveForum, template, "Inactive", sortOrder: 0);
        var blog = AddRegistration("Blog", className: BlogClassName);
        AddRule(blog, template, "Blog rule", sortOrder: 0);
        var forumWithoutEnabledRules = AddRegistration("Forum D");
        AddRule(forumWithoutEnabledRules, template, "Disabled", sortOrder: 0, isEnabled: false);
        AddRegistration("Forum E");
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetForumRegistrationsWithEnabledRulesAsync(
            CancellationToken.None
        );

        // Assert
        result.Count.ShouldBe(2);
        result[0].DistributionSiteRegistrationId.ShouldBe(firstForum.Id);
        result[0].Name.ShouldBe("Forum A");
        result[0].EnableAutomaticPosting.ShouldBeFalse();
        result[0].StripDotsForThreadSearch.ShouldBeFalse();
        result[0].EnabledRules.Select(rule => rule.Id).ShouldBe([firstForumRule.Id]);
        result[1].DistributionSiteRegistrationId.ShouldBe(secondForum.Id);
        result[1].Name.ShouldBe("Forum B");
        result[1].EnableAutomaticPosting.ShouldBeTrue();
        result[1].StripDotsForThreadSearch.ShouldBeTrue();
        result[1]
            .EnabledRules.Select(rule => rule.Id)
            .ShouldBe([secondForumFirstRule.Id, secondForumTiedRule.Id, secondForumLastRule.Id]);
        result[1].EnabledRules[0].ConditionJson.ShouldBe("{\"kind\":\"Café\"}");
        result[1].EnabledRules[0].ForumPostTemplateId.ShouldBe(template.Id);
    }

    [Test]
    public async Task GetQueuedReleaseIdsAsync_ReleasesWithDifferentUploadProgress_ReturnsReleasesWithUploadsNewerThanLastPost()
    {
        // Arrange
        var hoster = CreateHosterRegistration("Alpha", isActive: true);
        var neverPostedRelease = AddRelease("Bearcat.Release.001");
        AddUpload(
            AddUploadConfig(neverPostedRelease, hoster),
            new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc)
        );
        var postedAtSameTimeRelease = AddRelease(
            "Bearcat.Release.002",
            uploadsPostedAt: new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc)
        );
        AddUpload(
            AddUploadConfig(postedAtSameTimeRelease, hoster),
            new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc)
        );
        var reuploadedAfterPostRelease = AddRelease(
            "Bearcat.Release.003",
            uploadsPostedAt: new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc)
        );
        AddUpload(
            AddUploadConfig(reuploadedAfterPostRelease, hoster),
            new DateTime(2026, 9, 1, 10, 0, 0, 500, DateTimeKind.Utc)
        );
        var latestUploadPendingRelease = AddRelease("Bearcat.Release.004");
        var latestUploadPendingUploadConfig = AddUploadConfig(latestUploadPendingRelease, hoster);
        AddUpload(
            latestUploadPendingUploadConfig,
            new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc)
        );
        AddUpload(
            latestUploadPendingUploadConfig,
            uploadedAt: null,
            uploadState: UploadState.Pending,
            createdAt: new DateTime(2026, 9, 1, 9, 0, 0, 1, DateTimeKind.Utc)
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetQueuedReleaseIdsAsync(CancellationToken.None);

        // Assert
        result.ShouldBe([neverPostedRelease.Id, reuploadedAfterPostRelease.Id]);
    }

    [Test]
    public async Task GetReleasesForRoutingAsync_RequestedReleases_ReturnsReleasesWithClassificationAndReleaseGroup()
    {
        // Arrange
        var classifiedRelease = AddRelease("Bearcat.Release.001");
        classifiedRelease.Classification = new ReleaseClassification
        {
            Title = "Bearcat Release",
            Year = 2026,
            ContentType = ReleaseContentType.Movie,
            ClassifiedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var unclassifiedRelease = AddRelease("Bearcat.Release.002");
        AddRelease("Bearcat.Release.003");
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetReleasesForRoutingAsync(
            [classifiedRelease.Id, unclassifiedRelease.Id],
            CancellationToken.None
        );

        // Assert
        var releasesById = result.ToDictionary(release => release.Id);
        releasesById.Keys.ShouldBe(
            [classifiedRelease.Id, unclassifiedRelease.Id],
            ignoreOrder: true
        );
        releasesById[classifiedRelease.Id].Classification.ShouldNotBeNull();
        releasesById[classifiedRelease.Id].Classification!.Title.ShouldBe("Bearcat Release");
        releasesById[classifiedRelease.Id].Classification!.Year.ShouldBe(2026);
        releasesById[classifiedRelease.Id].ReleaseGroup.Name.ShouldBe("Bearcat.Release.001 group");
        releasesById[unclassifiedRelease.Id].Classification.ShouldBeNull();
        releasesById[unclassifiedRelease.Id]
            .ReleaseGroup.Name.ShouldBe("Bearcat.Release.002 group");
    }

    [Test]
    public async Task GetPostedLocationsAsync_RequestedReleases_ReturnsOnlyReleasePostedLocationsOrderedById()
    {
        // Arrange
        var template = AddForumPostTemplate("Release post");
        var registration = AddRegistration("Forum A");
        var release = AddRelease("Bearcat.Release.001");
        var releaseWithoutLocations = AddRelease("Bearcat.Release.002");
        var otherRelease = AddRelease("Bearcat.Release.003");
        var collection = AddReleaseCollection("Bearcat.Collection");
        var forumLocation = AddPostedLocation(
            "https://forum.example/threads/1",
            release: release,
            registration: registration,
            template: template,
            createdAt: new DateTime(2026, 9, 1, 23, 59, 59, 999, DateTimeKind.Utc),
            contentUpdatedAt: new DateTime(2026, 9, 2, 0, 0, 0, 1, DateTimeKind.Utc)
        );
        var manualLocation = AddPostedLocation(
            "https://blog.example/post/1",
            release: release,
            createdAt: new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc)
        );
        AddPostedLocation("https://forum.example/threads/2", release: otherRelease);
        AddPostedLocation("https://forum.example/threads/3", collection: collection);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetPostedLocationsAsync(
            [release.Id, releaseWithoutLocations.Id],
            CancellationToken.None
        );

        // Assert
        result.ShouldBe([
            new AutoPostPostedLocation(
                forumLocation.Id,
                release.Id,
                registration.Id,
                template.Id,
                "https://forum.example/threads/1",
                new DateTime(2026, 9, 1, 23, 59, 59, 999, DateTimeKind.Utc),
                new DateTime(2026, 9, 2, 0, 0, 0, 1, DateTimeKind.Utc)
            ),
            new AutoPostPostedLocation(
                manualLocation.Id,
                release.Id,
                null,
                null,
                "https://blog.example/post/1",
                new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc),
                null
            ),
        ]);
    }

    [Test]
    public async Task GetLatestUploadCompletionTimesAsync_SeveralCompletedUploadsPerRelease_ReturnsLatestCountedCompletionPerRelease()
    {
        // Arrange
        var activeHoster = CreateHosterRegistration("Alpha", isActive: true);
        var inactiveHoster = CreateHosterRegistration("Beta", isActive: false);
        var firstRelease = AddRelease("Bearcat.Release.001");
        var firstReleaseMainUploadConfig = AddUploadConfig(firstRelease, activeHoster);
        var firstReleaseMirrorUploadConfig = AddUploadConfig(firstRelease, activeHoster);
        AddUpload(
            firstReleaseMainUploadConfig,
            new DateTime(2026, 9, 3, 23, 59, 59, 999, DateTimeKind.Utc)
        );
        AddUpload(
            firstReleaseMainUploadConfig,
            new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Utc)
        );
        AddUpload(
            firstReleaseMirrorUploadConfig,
            new DateTime(2026, 9, 4, 0, 0, 0, 1, DateTimeKind.Utc)
        );
        AddUpload(
            firstReleaseMirrorUploadConfig,
            new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc)
        );
        AddUpload(
            firstReleaseMainUploadConfig,
            new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc),
            uploadState: UploadState.Failed
        );
        AddUpload(
            AddUploadConfig(firstRelease, inactiveHoster),
            new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc)
        );
        var collectionSlotUploadConfig = AddUploadConfig(firstRelease, activeHoster);
        collectionSlotUploadConfig.CollectionUploadSlot = new CollectionUploadSlot
        {
            ReleaseCollection = AddReleaseCollection("Bearcat.Collection"),
            Key = "forum-a",
            Name = "Forum A",
        };
        AddUpload(collectionSlotUploadConfig, new DateTime(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc));
        var secondRelease = AddRelease("Bearcat.Release.002");
        var secondReleaseUploadConfig = AddUploadConfig(secondRelease, activeHoster);
        AddUpload(
            secondReleaseUploadConfig,
            new DateTime(2026, 9, 2, 9, 59, 59, 900, DateTimeKind.Utc)
        );
        AddUpload(
            secondReleaseUploadConfig,
            new DateTime(2026, 9, 2, 10, 0, 0, 500, DateTimeKind.Utc)
        );
        AddUpload(secondReleaseUploadConfig, new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc));
        var pendingRelease = AddRelease("Bearcat.Release.003");
        AddUpload(
            AddUploadConfig(pendingRelease, activeHoster),
            uploadedAt: null,
            uploadState: UploadState.Pending
        );
        var notRequestedRelease = AddRelease("Bearcat.Release.004");
        AddUpload(
            AddUploadConfig(notRequestedRelease, activeHoster),
            new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc)
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetLatestUploadCompletionTimesAsync(
            [firstRelease.Id, secondRelease.Id, pendingRelease.Id],
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(
            new Dictionary<int, DateTime>
            {
                [firstRelease.Id] = new DateTime(2026, 9, 4, 0, 0, 0, 1, DateTimeKind.Utc),
                [secondRelease.Id] = new DateTime(2026, 9, 2, 10, 0, 0, 500, DateTimeKind.Utc),
            },
            ignoreOrder: true
        );
    }

    [Test]
    public async Task GetUpdateTargetAsync_ReleasePostedLocation_ReturnsReleaseAsTarget()
    {
        // Arrange
        var template = AddForumPostTemplate("Release post");
        var registration = AddRegistration("Forum A");
        var release = AddRelease("Bearcat.Release.001");
        var postedLocation = AddPostedLocation(
            "https://forum.example/threads/1",
            release: release,
            registration: registration,
            template: template
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetUpdateTargetAsync(
            postedLocation.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(
            new AutoPostUpdateTarget(
                postedLocation.Id,
                release.Id,
                "Bearcat.Release.001",
                release.Id,
                registration.Id,
                "Forum A",
                "https://forum.example/threads/1",
                template.Id
            )
        );
    }

    [Test]
    public async Task GetUpdateTargetAsync_CollectionPostedLocation_ReturnsCollectionAsTarget()
    {
        // Arrange
        var registration = AddRegistration("Forum A");
        var collection = AddReleaseCollection("Bearcat.Collection");
        var postedLocation = AddPostedLocation(
            "https://forum.example/threads/1",
            collection: collection,
            registration: registration
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetUpdateTargetAsync(
            postedLocation.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(
            new AutoPostUpdateTarget(
                postedLocation.Id,
                collection.Id,
                "Bearcat.Collection",
                null,
                registration.Id,
                "Forum A",
                "https://forum.example/threads/1",
                null
            )
        );
    }

    [Test]
    public async Task GetUpdateTargetAsync_PostedLocationWithoutRegistration_Throws()
    {
        // Arrange
        var release = AddRelease("Bearcat.Release.001");
        var postedLocation = AddPostedLocation("https://blog.example/post/1", release: release);
        await DbContext.SaveChangesAsync();

        // Act
        var action = () =>
            repository.GetUpdateTargetAsync(postedLocation.Id, CancellationToken.None);

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(action);
    }

    [Test]
    public async Task RecordPostedLocationAsync_ValidValues_StoresTrimmedUrlAndTimestamps()
    {
        // Arrange
        var template = AddForumPostTemplate("Release post");
        var registration = AddRegistration("Forum A");
        var release = AddRelease("Bearcat.Release.001");
        await DbContext.SaveChangesAsync();

        // Act
        await repository.RecordPostedLocationAsync(
            release.Id,
            registration.Id,
            "  https://forum.example/threads/1 ",
            template.Id,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var postedLocation = await DbContext.PostedLocations.SingleAsync();
        postedLocation.ReleaseId.ShouldBe(release.Id);
        postedLocation.ReleaseCollectionId.ShouldBeNull();
        postedLocation.DistributionSiteRegistrationId.ShouldBe(registration.Id);
        postedLocation.ForumPostTemplateId.ShouldBe(template.Id);
        postedLocation.Url.ShouldBe("https://forum.example/threads/1");
        postedLocation.CreatedAt.ShouldBe(Now);
        postedLocation.ContentUpdatedAt.ShouldBe(Now);
    }

    [Test]
    public async Task MarkPostedLocationUpdatedAsync_PostedLocationExists_UpdatesUrlTemplateAndContentUpdatedAt()
    {
        // Arrange
        var oldTemplate = AddForumPostTemplate("Old post");
        var newTemplate = AddForumPostTemplate("New post");
        var registration = AddRegistration("Forum A");
        var release = AddRelease("Bearcat.Release.001");
        var createdAt = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
        var postedLocation = AddPostedLocation(
            "https://forum.example/threads/1",
            release: release,
            registration: registration,
            template: oldTemplate,
            createdAt: createdAt,
            contentUpdatedAt: createdAt
        );
        var otherPostedLocation = AddPostedLocation(
            "https://forum.example/threads/2",
            release: release,
            registration: registration,
            template: oldTemplate,
            createdAt: createdAt,
            contentUpdatedAt: createdAt
        );
        await DbContext.SaveChangesAsync();

        // Act
        await repository.MarkPostedLocationUpdatedAsync(
            postedLocation.Id,
            " https://forum.example/threads/1-renamed ",
            newTemplate.Id,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var updatedPostedLocation = await DbContext.PostedLocations.SingleAsync(location =>
            location.Id == postedLocation.Id
        );
        updatedPostedLocation.Url.ShouldBe("https://forum.example/threads/1-renamed");
        updatedPostedLocation.ForumPostTemplateId.ShouldBe(newTemplate.Id);
        updatedPostedLocation.CreatedAt.ShouldBe(createdAt);
        updatedPostedLocation.ContentUpdatedAt.ShouldBe(Now);
        var unchangedPostedLocation = await DbContext.PostedLocations.SingleAsync(location =>
            location.Id == otherPostedLocation.Id
        );
        unchangedPostedLocation.ForumPostTemplateId.ShouldBe(oldTemplate.Id);
        unchangedPostedLocation.ContentUpdatedAt.ShouldBe(createdAt);
    }

    [Test]
    public async Task MarkReleasePostedAsync_ReleaseExists_SetsUploadsPostedAtOfReleaseOnly()
    {
        // Arrange
        var release = AddRelease("Bearcat.Release.001");
        var otherRelease = AddRelease("Bearcat.Release.002");
        await DbContext.SaveChangesAsync();

        // Act
        await repository.MarkReleasePostedAsync(release.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        (
            await DbContext.Releases.SingleAsync(candidate => candidate.Id == release.Id)
        ).UploadsPostedAt.ShouldBe(Now);
        (
            await DbContext.Releases.SingleAsync(candidate => candidate.Id == otherRelease.Id)
        ).UploadsPostedAt.ShouldBeNull();
    }

    private ForumPostTemplate AddForumPostTemplate(string name)
    {
        var template = new ForumPostTemplate
        {
            Name = name,
            Type = ForumPostTemplateType.Release,
            TemplateBody = string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        DbContext.ForumPostTemplates.Add(template);

        return template;
    }

    private DistributionSiteRegistration AddRegistration(
        string name,
        string className = ForumClassName,
        bool isActive = true,
        bool enableAutomaticPosting = false,
        bool stripDotsForThreadSearch = true
    )
    {
        var registration = new DistributionSiteRegistration
        {
            Name = name,
            DistributionSiteClassName = className,
            SerializedConfig = "{}",
            IsActive = isActive,
            EnableAutomaticPosting = enableAutomaticPosting,
            StripDotsForThreadSearch = stripDotsForThreadSearch,
        };

        DbContext.DistributionSiteRegistrations.Add(registration);

        return registration;
    }

    private ForumPostingRule AddRule(
        DistributionSiteRegistration registration,
        ForumPostTemplate template,
        string name,
        int sortOrder,
        bool isEnabled = true,
        string conditionJson = "{}"
    )
    {
        var rule = new ForumPostingRule
        {
            DistributionSiteRegistration = registration,
            SortOrder = sortOrder,
            Name = name,
            ConditionJson = conditionJson,
            TargetNodeId = "1",
            TargetPathSnapshot = "Forum",
            ForumPostTemplate = template,
            IsEnabled = isEnabled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        DbContext.ForumPostingRules.Add(rule);

        return rule;
    }

    private Release AddRelease(string name, DateTime? uploadsPostedAt = null)
    {
        var release = new Release
        {
            Name = name,
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/{name}",
            UploadsPostedAt = uploadsPostedAt,
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

    private ReleaseCollection AddReleaseCollection(string name)
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
            CreatedAt = DateTime.UtcNow,
        };

        DbContext.ReleaseCollections.Add(collection);

        return collection;
    }

    private PostedLocation AddPostedLocation(
        string url,
        Release? release = null,
        ReleaseCollection? collection = null,
        DistributionSiteRegistration? registration = null,
        ForumPostTemplate? template = null,
        DateTime? createdAt = null,
        DateTime? contentUpdatedAt = null
    )
    {
        var postedLocation = new PostedLocation
        {
            Release = release,
            ReleaseCollection = collection,
            DistributionSiteRegistration = registration,
            ForumPostTemplate = template,
            Url = url,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            ContentUpdatedAt = contentUpdatedAt,
        };

        DbContext.PostedLocations.Add(postedLocation);

        return postedLocation;
    }

    private static HosterRegistration CreateHosterRegistration(string name, bool isActive)
    {
        return new HosterRegistration
        {
            Name = name,
            SerializedConfig = "{}",
            HosterClassName = $"{name}Hoster",
            IsActive = isActive,
        };
    }

    private UploadConfig AddUploadConfig(Release release, HosterRegistration hosterRegistration)
    {
        var uploadConfig = new UploadConfig
        {
            Release = release,
            ArchiveConfig = new ArchiveConfig
            {
                Release = release,
                Name = "Main archive",
                ArchiveFilesBasePath = "/tmp/archive",
                ArchiverName = "zip",
                ArchiveFileSizeMb = 512,
            },
            HosterRegistration = hosterRegistration,
            Name = $"{hosterRegistration.Name} upload",
        };

        DbContext.UploadConfigs.Add(uploadConfig);

        return uploadConfig;
    }

    private void AddUpload(
        UploadConfig uploadConfig,
        DateTime? uploadedAt,
        UploadState uploadState = UploadState.Completed,
        DateTime? createdAt = null
    )
    {
        DbContext.Uploads.Add(
            new Upload
            {
                UploadConfig = uploadConfig,
                CreatedAt = createdAt ?? uploadedAt ?? DateTime.UtcNow,
                UploadedAt = uploadedAt,
                UploadState = uploadState,
                OnlineState = OnlineState.Online,
            }
        );
    }
}
