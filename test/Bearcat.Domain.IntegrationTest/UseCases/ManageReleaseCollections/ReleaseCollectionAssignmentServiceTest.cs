using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseCollections;

public class ReleaseCollectionAssignmentServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private ReleaseCollectionRepository repository = null!;
    private ReleaseCollectionAssignmentService assignmentService = null!;

    [SetUp]
    public void Setup()
    {
        repository = new ReleaseCollectionRepository(
            dbRead: DbContext,
            dbWrite: DbContext,
            metadataDatabaseFactory: Mock.Of<IMediaMetadataDatabaseFactory>()
        );
        assignmentService = new ReleaseCollectionAssignmentService(
            writeRepository: repository,
            timeProvider: CreateTimeProvider(),
            logger: NullLogger<ReleaseCollectionAssignmentService>.Instance
        );
    }

    [Test]
    public async Task AssignFromTemplateAsync_CollectionWithSameNameInSameReleaseGroup_AssignsReleaseToExistingCollection()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Series");
        var existingCollection = await AddCollectionAsync(
            releaseGroup,
            name: "HOSTAGE s01",
            key: "hostage.s01.other"
        );
        var release = new Release { Name = "Hostage.S01E01.German.1080p.WEB.x264-GRP" };

        // Act
        await assignmentService.AssignFromTemplateAsync(
            release: release,
            releaseTemplate: CreateReleaseTemplate(releaseGroup.Id),
            uploadConfigMatches: [],
            cancellationToken: CancellationToken.None
        );
        await repository.SaveChangesAsync();

        // Assert
        release.ReleaseCollection.ShouldBeSameAs(existingCollection);
        (await DbContext.ReleaseCollections.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task AssignFromTemplateAsync_CollectionWithSameNameInOtherReleaseGroup_LeavesReleaseUnassigned()
    {
        // Arrange
        var otherReleaseGroup = await AddReleaseGroupAsync("Other series");
        var releaseGroup = await AddReleaseGroupAsync("Series");
        await AddCollectionAsync(otherReleaseGroup, name: "Hostage S01", key: "hostage.s01");
        var release = new Release { Name = "Hostage.S01E01.German.1080p.WEB.x264-GRP" };

        // Act
        await assignmentService.AssignFromTemplateAsync(
            release: release,
            releaseTemplate: CreateReleaseTemplate(releaseGroup.Id),
            uploadConfigMatches: [],
            cancellationToken: CancellationToken.None
        );
        await repository.SaveChangesAsync();

        // Assert
        release.ReleaseCollection.ShouldBeNull();
        (await DbContext.ReleaseCollections.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task AssignFromTemplateAsync_ReleasesOfSameBatchWithDifferentKeysAndSameName_CreatesOneCollection()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Series");
        var releaseTemplate = CreateReleaseTemplate(releaseGroup.Id);
        var firstRelease = new Release { Name = "Hostage.S01E01.German.1080p.WEB.x264-GRP" };
        var secondRelease = new Release { Name = "Hostage.S01E02.German.720p.WEB.x264-GRP" };

        // Act
        await assignmentService.AssignFromTemplateAsync(
            release: firstRelease,
            releaseTemplate: releaseTemplate,
            uploadConfigMatches: [],
            cancellationToken: CancellationToken.None
        );
        await assignmentService.AssignFromTemplateAsync(
            release: secondRelease,
            releaseTemplate: releaseTemplate,
            uploadConfigMatches: [],
            cancellationToken: CancellationToken.None
        );
        await repository.SaveChangesAsync();

        // Assert
        firstRelease.ReleaseCollection.ShouldNotBeNull();
        secondRelease.ReleaseCollection.ShouldBeSameAs(firstRelease.ReleaseCollection);
        (await DbContext.ReleaseCollections.CountAsync()).ShouldBe(1);
    }

    private static ReleaseTemplate CreateReleaseTemplate(int releaseGroupId)
    {
        return new ReleaseTemplate
        {
            ReleaseGroupId = releaseGroupId,
            ReleaseContentType = ReleaseContentType.TvShowEpisode,
            ReleaseCollectionDetectionMode = ReleaseCollectionDetectionMode.SeriesEpisodePattern,
            ReleaseCollectionKeyTemplate = "{title}.S{season}.{variant}",
            ReleaseCollectionNameTemplate = "{title:dotToSpace} S{season}",
        };
    }

    private async Task<ReleaseCollection> AddCollectionAsync(
        ReleaseGroup releaseGroup,
        string name,
        string key
    )
    {
        var releaseCollection = new ReleaseCollection
        {
            ReleaseGroup = releaseGroup,
            Name = name,
            Key = key,
            ReleaseContentType = ReleaseContentType.TvShowEpisode,
            CreatedAt = DateTime.UtcNow,
        };

        DbContext.ReleaseCollections.Add(releaseCollection);
        await DbContext.SaveChangesAsync();

        return releaseCollection;
    }

    private async Task<ReleaseGroup> AddReleaseGroupAsync(string name)
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = name,
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };

        DbContext.ReleaseGroups.Add(releaseGroup);
        await DbContext.SaveChangesAsync();

        return releaseGroup;
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }
}
