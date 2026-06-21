using Bearcat.Abstractions.SeriesDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseCollections;

public class ReleaseCollectionPostQueueTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private ReleaseCollectionRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        repository = new ReleaseCollectionRepository(
            dbContext,
            dbContext,
            Mock.Of<ISeriesDatabaseFactory>()
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task GetPostQueueAsync_GroupsBySlot_AndExcludesPostedAndStandaloneOnlyCollections()
    {
        // Arrange
        var openCollection = await AddCollectionAsync(
            "Open.Collection",
            slotName: "1080p",
            linkCount: 2,
            useSlot: true,
            withCollectionContainer: true
        );

        await AddCollectionAsync(
            "Posted.Collection",
            slotName: "1080p",
            linkCount: 1,
            useSlot: true,
            uploadsPostedAt: DateTime.UtcNow.AddDays(1)
        );

        await AddCollectionAsync(
            "Standalone.Only.Collection",
            slotName: "1080p",
            linkCount: 1,
            useSlot: false
        );

        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);
        var count = await repository.CountPostQueueAsync(CancellationToken.None);

        // Assert
        count.ShouldBe(1);

        var item = result.ShouldHaveSingleItem();
        item.ReleaseCollectionId.ShouldBe(openCollection.Id);

        var slotGroup = item.SlotGroups.ShouldHaveSingleItem();
        slotGroup.SlotName.ShouldBe("1080p");

        var hoster = slotGroup.Hosters.ShouldHaveSingleItem();
        hoster.HosterRegistrationName.ShouldBe("Open.Collection hoster");
        hoster.LinkCount.ShouldBe(2);

        var container = slotGroup.Containers.ShouldHaveSingleItem();
        container.LinkCrypterRegistrationName.ShouldBe("Open.Collection crypter");
        container.Count.ShouldBe(1);
    }

    [Test]
    public async Task GetPostQueueAsync_SlotConfigWithLatestPendingUpload_HidesCollection()
    {
        // Arrange
        await AddPostQueueCollectionAsync(
            "Pending.Collection",
            [
                new CollectionReleaseSpec([
                    new CollectionConfigSpec([new CollectionUploadSpec(UploadState.Completed, 10)]),
                    new CollectionConfigSpec([
                        new CollectionUploadSpec(
                            UploadState.Pending,
                            5,
                            HasUploadedAt: false,
                            FileCount: 0
                        ),
                    ]),
                ]),
            ]
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);
        var count = await repository.CountPostQueueAsync(CancellationToken.None);

        // Assert
        count.ShouldBe(0);
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetPostQueueAsync_SlotConfigWithoutUploads_HidesCollection()
    {
        // Arrange
        await AddPostQueueCollectionAsync(
            "Missing.Collection",
            [
                new CollectionReleaseSpec([
                    new CollectionConfigSpec([new CollectionUploadSpec(UploadState.Completed, 10)]),
                    new CollectionConfigSpec([]),
                ]),
            ]
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);
        var count = await repository.CountPostQueueAsync(CancellationToken.None);

        // Assert
        count.ShouldBe(0);
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetPostQueueAsync_SecondReleaseInCollectionIncomplete_HidesCollection()
    {
        // Arrange
        await AddPostQueueCollectionAsync(
            "MultiRelease.Collection",
            [
                new CollectionReleaseSpec([
                    new CollectionConfigSpec([new CollectionUploadSpec(UploadState.Completed, 10)]),
                ]),
                new CollectionReleaseSpec([
                    new CollectionConfigSpec([
                        new CollectionUploadSpec(
                            UploadState.Pending,
                            5,
                            HasUploadedAt: false,
                            FileCount: 0
                        ),
                    ]),
                ]),
            ]
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);
        var count = await repository.CountPostQueueAsync(CancellationToken.None);

        // Assert
        count.ShouldBe(0);
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetPostQueueAsync_LatestSlotUploadCompletedAfterCanceledReupload_ShowsCollection()
    {
        // Arrange
        var collection = await AddPostQueueCollectionAsync(
            "Reupload.Collection",
            [
                new CollectionReleaseSpec([
                    new CollectionConfigSpec([
                        new CollectionUploadSpec(
                            UploadState.Canceled,
                            60,
                            HasUploadedAt: false,
                            FileCount: 0
                        ),
                        new CollectionUploadSpec(UploadState.Completed, 5),
                    ]),
                ]),
            ]
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);
        var count = await repository.CountPostQueueAsync(CancellationToken.None);

        // Assert
        count.ShouldBe(1);
        result.ShouldHaveSingleItem().ReleaseCollectionId.ShouldBe(collection.Id);
    }

    [Test]
    public async Task GetPostQueueAsync_InactiveHosterSlotConfigWithoutUploads_IsIgnored_ShowsCollection()
    {
        // Arrange
        var collection = await AddPostQueueCollectionAsync(
            "InactiveSlot.Collection",
            [
                new CollectionReleaseSpec([
                    new CollectionConfigSpec([new CollectionUploadSpec(UploadState.Completed, 10)]),
                    new CollectionConfigSpec([], HosterActive: false),
                ]),
            ]
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);
        var count = await repository.CountPostQueueAsync(CancellationToken.None);

        // Assert
        count.ShouldBe(1);
        result.ShouldHaveSingleItem().ReleaseCollectionId.ShouldBe(collection.Id);
    }

    [Test]
    public async Task GetPostQueueAsync_StandaloneConfigIncomplete_DoesNotHideCollection()
    {
        // Arrange
        var collection = await AddPostQueueCollectionAsync(
            "StandaloneNoise.Collection",
            [
                new CollectionReleaseSpec([
                    new CollectionConfigSpec([new CollectionUploadSpec(UploadState.Completed, 10)]),
                    new CollectionConfigSpec(
                        [
                            new CollectionUploadSpec(
                                UploadState.Pending,
                                5,
                                HasUploadedAt: false,
                                FileCount: 0
                            ),
                        ],
                        UseSlot: false
                    ),
                ]),
            ]
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);
        var count = await repository.CountPostQueueAsync(CancellationToken.None);

        // Assert
        count.ShouldBe(1);
        result.ShouldHaveSingleItem().ReleaseCollectionId.ShouldBe(collection.Id);
    }

    private async Task<ReleaseCollection> AddPostQueueCollectionAsync(
        string name,
        IReadOnlyList<CollectionReleaseSpec> releases,
        DateTime? uploadsPostedAt = null
    )
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = $"{name} group",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
            Releases = [],
        };

        var collection = new ReleaseCollection
        {
            ReleaseGroup = releaseGroup,
            Key = $"key-{Guid.NewGuid():N}",
            Name = name,
            ReleaseContentType = ReleaseContentType.TvShowEpisode,
            CreatedAt = DateTime.UtcNow,
            UploadsPostedAt = uploadsPostedAt,
        };

        dbContext.AddRange(releaseGroup, collection);

        var releaseIndex = 0;
        foreach (var releaseSpec in releases)
        {
            releaseIndex++;
            var release = new Release
            {
                Name = $"{name}.S01E{releaseIndex:00}-GRP",
                CreatedAt = DateTime.UtcNow,
                ReleaseType = ReleaseType.Managed,
                ReleaseFolderPath = $"/tmp/{name}/{releaseIndex}",
                ReleaseGroup = releaseGroup,
                ReleaseCollection = collection,
            };
            dbContext.Add(release);

            var configIndex = 0;
            foreach (var configSpec in releaseSpec.Configs)
            {
                configIndex++;
                var archiveConfig = new ArchiveConfig
                {
                    Release = release,
                    Name = $"{name} archive {releaseIndex}-{configIndex}",
                    ArchiveFilesBasePath = "/tmp/archives",
                    ArchiverName = "RarArchiver",
                    ArchiveFileSizeMb = 100,
                };
                var hosterRegistration = new HosterRegistration
                {
                    Name = $"{name} hoster {releaseIndex}-{configIndex}",
                    SerializedConfig = "{}",
                    HosterClassName = "Rapidgator",
                    IsActive = configSpec.HosterActive,
                };

                CollectionUploadSlot? uploadSlot = null;
                if (configSpec.UseSlot)
                {
                    uploadSlot = new CollectionUploadSlot
                    {
                        ReleaseCollection = collection,
                        Key = $"{configSpec.SlotName}-{Guid.NewGuid():N}".ToLowerInvariant(),
                        Name = configSpec.SlotName,
                        UploadConfigs = [],
                    };
                    dbContext.Add(uploadSlot);
                }

                var uploadConfig = new UploadConfig
                {
                    Release = release,
                    ArchiveConfig = archiveConfig,
                    HosterRegistration = hosterRegistration,
                    CollectionUploadSlot = uploadSlot,
                    Name = $"{name} upload {releaseIndex}-{configIndex}",
                    LinkCrypters = [],
                };
                dbContext.AddRange(archiveConfig, hosterRegistration, uploadConfig);

                foreach (var uploadSpec in configSpec.Uploads)
                {
                    AddCollectionUpload(name, uploadConfig, uploadSpec);
                }
            }
        }

        await dbContext.SaveChangesAsync();

        return collection;
    }

    private void AddCollectionUpload(
        string name,
        UploadConfig uploadConfig,
        CollectionUploadSpec spec
    )
    {
        var createdAt = DateTime.UtcNow.AddMinutes(-spec.CreatedMinutesAgo);
        var upload = new Upload
        {
            UploadConfig = uploadConfig,
            CreatedAt = createdAt,
            UploadedAt = spec.HasUploadedAt ? createdAt : null,
            UploadState = spec.State,
            OnlineState = OnlineState.Online,
            UploadedFiles = [],
        };

        if (spec.FileCount > 0)
        {
            var archive = new Archive
            {
                ArchiveConfig = uploadConfig.ArchiveConfig,
                ArchiveFolderPath = "/tmp/archives",
                CreatedAt = createdAt,
                ArchiveState = ArchiveState.Created,
                ArchiveFileSizeMb = 100,
            };
            upload.Archive = archive;
            dbContext.Add(archive);

            for (var i = 0; i < spec.FileCount; i++)
            {
                var archiveFile = new ArchiveFile
                {
                    Archive = archive,
                    FullFileName = $"{name}.{uploadConfig.Name}.part{i:00}-{Guid.NewGuid():N}.rar",
                    UploadedFiles = [],
                };
                upload.UploadedFiles.Add(
                    new UploadedFile
                    {
                        Upload = upload,
                        ArchiveFile = archiveFile,
                        HosterFileLink = $"https://hoster.example/{name}/{Guid.NewGuid():N}",
                        OnlineState = OnlineState.Online,
                        CreatedAt = createdAt,
                    }
                );
                dbContext.Add(archiveFile);
            }
        }

        dbContext.Add(upload);
    }

    private sealed record CollectionUploadSpec(
        UploadState State,
        int CreatedMinutesAgo,
        bool HasUploadedAt = true,
        int FileCount = 1
    );

    private sealed record CollectionConfigSpec(
        IReadOnlyList<CollectionUploadSpec> Uploads,
        bool UseSlot = true,
        bool HosterActive = true,
        string SlotName = "1080p"
    );

    private sealed record CollectionReleaseSpec(IReadOnlyList<CollectionConfigSpec> Configs);

    private async Task<ReleaseCollection> AddCollectionAsync(
        string name,
        string slotName,
        int linkCount,
        bool useSlot,
        bool withCollectionContainer = false,
        DateTime? uploadsPostedAt = null
    )
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = $"{name} group",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
            Releases = [],
        };

        var collection = new ReleaseCollection
        {
            ReleaseGroup = releaseGroup,
            Key = $"key-{Guid.NewGuid():N}",
            Name = name,
            ReleaseContentType = ReleaseContentType.TvShowEpisode,
            CreatedAt = DateTime.UtcNow,
            UploadsPostedAt = uploadsPostedAt,
        };

        var release = new Release
        {
            Name = $"{name}.S01E01-GRP",
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/{name}",
            ReleaseGroup = releaseGroup,
            ReleaseCollection = collection,
        };

        var archiveConfig = new ArchiveConfig
        {
            Release = release,
            Name = $"{name} archive",
            ArchiveFilesBasePath = "/tmp/archives",
            ArchiverName = "RarArchiver",
            ArchiveFileSizeMb = 100,
        };

        var hosterRegistration = new HosterRegistration
        {
            Name = $"{name} hoster",
            SerializedConfig = "{}",
            HosterClassName = "Rapidgator",
            IsActive = true,
        };

        CollectionUploadSlot? uploadSlot = null;
        if (useSlot)
        {
            uploadSlot = new CollectionUploadSlot
            {
                ReleaseCollection = collection,
                Key = slotName.ToLowerInvariant(),
                Name = slotName,
                UploadConfigs = [],
            };
        }

        var uploadConfig = new UploadConfig
        {
            Release = release,
            HosterRegistration = hosterRegistration,
            ArchiveConfig = archiveConfig,
            CollectionUploadSlot = uploadSlot,
            Name = $"{name} upload",
            LinkCrypters = [],
        };

        var archive = new Archive
        {
            ArchiveConfig = archiveConfig,
            ArchiveFolderPath = "/tmp/archives",
            CreatedAt = DateTime.UtcNow,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 100,
        };

        var upload = new Upload
        {
            UploadConfig = uploadConfig,
            Archive = archive,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            UploadedAt = DateTime.UtcNow,
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Online,
            UploadedFiles = [],
        };

        for (var i = 0; i < linkCount; i++)
        {
            var archiveFile = new ArchiveFile
            {
                Archive = archive,
                FullFileName = $"{name}.part{i:00}.rar",
                UploadedFiles = [],
            };
            upload.UploadedFiles.Add(
                new UploadedFile
                {
                    Upload = upload,
                    ArchiveFile = archiveFile,
                    HosterFileLink = $"https://hoster.example/{name}/{i}",
                    OnlineState = OnlineState.Online,
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }

        dbContext.AddRange(releaseGroup, collection, release, archiveConfig, hosterRegistration);
        if (uploadSlot is not null)
        {
            dbContext.Add(uploadSlot);
        }

        dbContext.AddRange(uploadConfig, archive, upload);

        if (withCollectionContainer && uploadSlot is not null)
        {
            var linkCrypterRegistration = new LinkCrypterRegistration
            {
                Name = $"{name} crypter",
                LinkCrypterClassName = "FileCrypt",
                SerializedConfig = "{}",
                IsActive = true,
            };

            var container = new LinkCrypterContainer
            {
                Scope = LinkCrypterContainerScope.ReleaseCollection,
                CollectionUploadSlot = uploadSlot,
                LinkCrypterRegistration = linkCrypterRegistration,
                ContainerUrl = $"https://filecrypt.example/{name}",
                State = LinkCrypterContainerState.Created,
                CreatedAt = DateTime.UtcNow,
            };

            dbContext.AddRange(linkCrypterRegistration, container);
        }

        await dbContext.SaveChangesAsync();

        return collection;
    }
}
