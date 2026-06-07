using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseCollections;

public class ReleaseCollectionServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private ReleaseCollectionService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        service = new ReleaseCollectionService(
            new ReleaseCollectionRepository(dbContext, dbContext),
            CreateTimeProvider()
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task UpdateSharedLinkCryptersAsync_CollectionSlotHasMultipleUploadConfigs_UpdatesAllUploadConfigs()
    {
        // Arrange
        var seed = await AddReleaseCollectionWithUploadSlotAsync();

        // Act
        await service.UpdateSharedLinkCryptersAsync(
            seed.CollectionUploadSlotId,
            [seed.LinkCrypterRegistrationId],
            CancellationToken.None
        );

        // Assert
        dbContext.ChangeTracker.Clear();
        var uploadConfigs = await dbContext
            .UploadConfigs.Include(uploadConfig => uploadConfig.LinkCrypters)
            .Where(uploadConfig => uploadConfig.CollectionUploadSlotId == seed.CollectionUploadSlotId)
            .OrderBy(uploadConfig => uploadConfig.Name)
            .ToListAsync();

        uploadConfigs.Count.ShouldBe(2);
        uploadConfigs
            .Select(uploadConfig => uploadConfig.LinkCrypters.Single())
            .ShouldAllBe(linkCrypter =>
                linkCrypter.LinkCrypterRegistrationId == seed.LinkCrypterRegistrationId
                && linkCrypter.ContainerScope == LinkCrypterContainerScope.ReleaseCollection
            );

        var collectionRepository = new ReleaseCollectionRepository(dbContext, dbContext);
        var detail = await collectionRepository.GetDetailAsync(seed.ReleaseCollectionId);
        var sharedLinkCrypter = detail!.UploadSlots.Single().SharedLinkCrypters.Single();

        sharedLinkCrypter.LinkCrypterRegistrationName.ShouldBe("ShareCrypt");
        sharedLinkCrypter.UploadConfigCount.ShouldBe(2);

        await service.UpdateSharedLinkCryptersAsync(
            seed.CollectionUploadSlotId,
            [],
            CancellationToken.None
        );

        dbContext.ChangeTracker.Clear();
        var hasSharedLinkCrypters = await dbContext.UploadConfigLinkCrypters.AnyAsync(linkCrypter =>
            linkCrypter.ContainerScope == LinkCrypterContainerScope.ReleaseCollection
        );

        hasSharedLinkCrypters.ShouldBeFalse();
    }

    private async Task<ReleaseCollectionUploadSlotSeed> AddReleaseCollectionWithUploadSlotAsync()
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = "Series",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };
        var releaseCollection = new ReleaseCollection
        {
            ReleaseGroup = releaseGroup,
            Key = "hostage-s01",
            Name = "Hostage.S01.German.AC3.DL.1080p.Web.x265-FuN.mkv",
            CreatedAt = DateTime.UtcNow,
        };
        var collectionUploadSlot = new CollectionUploadSlot
        {
            ReleaseCollection = releaseCollection,
            Key = "forum-a-rg-passworded",
            Name = "Forum A Rapidgator passworded",
            PasswordPolicy = CollectionUploadSlotPasswordPolicy.Ignore,
        };
        var linkCrypterRegistration = new LinkCrypterRegistration
        {
            Name = "ShareCrypt",
            LinkCrypterClassName = "ShareCryptLinkCrypter",
            SerializedConfig = "{}",
            IsActive = true,
        };

        dbContext.LinkCrypterRegistrations.Add(linkCrypterRegistration);
        dbContext.UploadConfigs.AddRange(
            CreateUploadConfig(releaseGroup, releaseCollection, collectionUploadSlot, "Episode 1"),
            CreateUploadConfig(releaseGroup, releaseCollection, collectionUploadSlot, "Episode 2")
        );
        await dbContext.SaveChangesAsync();

        return new ReleaseCollectionUploadSlotSeed(
            releaseCollection.Id,
            collectionUploadSlot.Id,
            linkCrypterRegistration.Id
        );
    }

    private static UploadConfig CreateUploadConfig(
        ReleaseGroup releaseGroup,
        ReleaseCollection releaseCollection,
        CollectionUploadSlot collectionUploadSlot,
        string releaseName
    )
    {
        var release = new Release
        {
            Name = releaseName,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/{releaseName}",
            ReleaseGroup = releaseGroup,
            ReleaseCollection = releaseCollection,
        };
        var archiveConfig = new ArchiveConfig
        {
            Release = release,
            Name = $"{releaseName} archive",
            ArchiveFilesBasePath = "/tmp/archive",
            ArchiverName = "rar",
            ArchiveNamePrefix = releaseName,
            ArchiveFileSizeMb = 512,
        };
        var hosterRegistration = new HosterRegistration
        {
            Name = $"{releaseName} hoster",
            SerializedConfig = "{}",
            HosterClassName = "RapidgatorHoster",
            IsActive = true,
        };

        return new UploadConfig
        {
            Release = release,
            ArchiveConfig = archiveConfig,
            HosterRegistration = hosterRegistration,
            CollectionUploadSlot = collectionUploadSlot,
            Name = $"{releaseName} upload",
            LinksDistributedTo = [],
            LinkCrypters = [],
        };
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }

    private sealed record ReleaseCollectionUploadSlotSeed(
        int ReleaseCollectionId,
        int CollectionUploadSlotId,
        int LinkCrypterRegistrationId
    );
}
