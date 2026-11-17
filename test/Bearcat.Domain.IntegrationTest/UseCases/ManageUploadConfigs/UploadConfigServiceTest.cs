using Bearcat.Domain.Entities;
using Bearcat.Domain.IntegrationTest.Shared.UploadSources;
using Bearcat.Domain.UseCases.ManageUploadConfigs;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;
using Bearcat.Domain.UseCases.ManageUploadConfigs.UploadSources;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageUploadConfigs;

public class UploadConfigServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private UploadConfigService service = null!;

    [SetUp]
    public void Setup()
    {
        service = new UploadConfigService(
            new UploadConfigWriteRepository(DbContext),
            TestUploadSourceValidator.Create(DbContext)
        );
    }

    [Test]
    public async Task CreateAsync_VideoFilesForManagedMovieWithVideoHoster_PersistsVideoUploadConfigWithoutArchiveConfig()
    {
        // Arrange
        var seed = await AddUploadConfigDependenciesAsync(
            hosterClassName: TestUploadSourceValidator.VideoHosterClassName
        );

        // Act
        await service.CreateAsync(
            seed.ReleaseId,
            "Video upload",
            UploadTarget.ForHosterRegistration(seed.HosterRegistrationId),
            UploadSource.VideoFiles,
            null,
            false,
            CancellationToken.None
        );

        // Assert
        var uploadConfig = await DbContext.UploadConfigs.SingleAsync();

        uploadConfig.UploadSource.ShouldBe(UploadSource.VideoFiles);
        uploadConfig.ArchiveConfigId.ShouldBeNull();
    }

    [TestCase(
        UploadSource.VideoFiles,
        ReleaseType.Managed,
        ReleaseContentType.Other,
        TestUploadSourceValidator.VideoHosterClassName
    )]
    [TestCase(
        UploadSource.VideoFiles,
        ReleaseType.Unmanaged,
        ReleaseContentType.Movie,
        TestUploadSourceValidator.VideoHosterClassName
    )]
    [TestCase(UploadSource.VideoFiles, ReleaseType.Managed, ReleaseContentType.Movie, "TestHoster")]
    [TestCase(
        UploadSource.ArchiveFiles,
        ReleaseType.Managed,
        ReleaseContentType.Movie,
        TestUploadSourceValidator.VideoHosterClassName
    )]
    public async Task CreateAsync_UploadSourceNotAllowedForReleaseOrHoster_ThrowsInvalidUploadSourceException(
        UploadSource uploadSource,
        ReleaseType releaseType,
        ReleaseContentType releaseContentType,
        string hosterClassName
    )
    {
        // Arrange
        var seed = await AddUploadConfigDependenciesAsync(
            releaseType: releaseType,
            releaseContentType: releaseContentType,
            hosterClassName: hosterClassName
        );

        // Act
        var action = () =>
            service.CreateAsync(
                seed.ReleaseId,
                "Upload",
                UploadTarget.ForHosterRegistration(seed.HosterRegistrationId),
                uploadSource,
                uploadSource == UploadSource.ArchiveFiles ? seed.ArchiveConfigId : null,
                false,
                CancellationToken.None
            );

        // Assert
        await Should.ThrowAsync<InvalidUploadSourceException>(action);
        (await DbContext.UploadConfigs.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task UpdateAsync_HosterRegistrationChanged_MovesOnlyUploadsNotStartedOnHosterToNewHosterRegistration()
    {
        // Arrange
        var firstSeed = await AddUploadConfigDependenciesAsync();
        var uploadConfig = await AddUploadConfigAsync(firstSeed);
        var secondSeed = await AddUploadConfigDependenciesAsync("Second");
        var archive = await AddArchiveAsync(firstSeed.ArchiveConfigId);
        var waitingUpload = await AddUploadAsync(uploadConfig, UploadState.WaitingForArchive);
        var pendingUpload = await AddUploadAsync(uploadConfig, UploadState.Pending);
        var pendingUploadWithUploadedFile = await AddUploadAsync(
            uploadConfig,
            UploadState.Pending,
            archive.Id
        );
        var failedUpload = await AddUploadAsync(uploadConfig, UploadState.Failed, archive.Id);
        var completedUpload = await AddUploadAsync(uploadConfig, UploadState.Completed, archive.Id);
        DbContext.ChangeTracker.Clear();

        // Act
        await service.UpdateAsync(
            uploadConfig.Id,
            uploadConfig.Name,
            UploadTarget.ForHosterRegistration(secondSeed.HosterRegistrationId),
            firstSeed.ArchiveConfigId,
            false,
            CancellationToken.None
        );

        // Assert
        var hosterRegistrationIdsByUploadId = await DbContext
            .Uploads.Select(upload => new { upload.Id, upload.HosterRegistrationId })
            .ToDictionaryAsync(upload => upload.Id, upload => upload.HosterRegistrationId);
        hosterRegistrationIdsByUploadId.ShouldBe(
            new Dictionary<int, int?>
            {
                [waitingUpload.Id] = secondSeed.HosterRegistrationId,
                [pendingUpload.Id] = secondSeed.HosterRegistrationId,
                [pendingUploadWithUploadedFile.Id] = firstSeed.HosterRegistrationId,
                [failedUpload.Id] = firstSeed.HosterRegistrationId,
                [completedUpload.Id] = firstSeed.HosterRegistrationId,
            },
            ignoreOrder: true
        );
    }

    [Test]
    public async Task UpdateAsync_HosterRegistrationChanged_ClearsHosterFolderOfUploadsNotStartedOnHoster()
    {
        // Arrange
        var firstSeed = await AddUploadConfigDependenciesAsync();
        var uploadConfig = await AddUploadConfigAsync(firstSeed);
        var secondSeed = await AddUploadConfigDependenciesAsync("Second");
        var pendingUpload = await AddUploadAsync(uploadConfig, UploadState.Pending);
        pendingUpload.HosterFolderId = "folder-on-old-hoster";
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        await service.UpdateAsync(
            uploadConfig.Id,
            uploadConfig.Name,
            UploadTarget.ForHosterRegistration(secondSeed.HosterRegistrationId),
            firstSeed.ArchiveConfigId,
            false,
            CancellationToken.None
        );

        // Assert
        var upload = await DbContext.Uploads.SingleAsync();
        upload.HosterFolderId.ShouldBeNull();
        upload.HosterRegistrationId.ShouldBe(secondSeed.HosterRegistrationId);
    }

    [Test]
    public async Task UpdateAsync_HosterRegistrationChangedToHosterAccountPool_UnassignsOnlyUploadsNotStartedOnHoster()
    {
        // Arrange
        var seed = await AddUploadConfigDependenciesAsync();
        var uploadConfig = await AddUploadConfigAsync(seed);
        var hosterAccountPool = await AddHosterAccountPoolAsync(seed);
        var archive = await AddArchiveAsync(seed.ArchiveConfigId);
        var waitingUpload = await AddUploadAsync(uploadConfig, UploadState.WaitingForArchive);
        var pendingUploadWithUploadedFile = await AddUploadAsync(
            uploadConfig,
            UploadState.Pending,
            archive.Id
        );
        var completedUpload = await AddUploadAsync(uploadConfig, UploadState.Completed, archive.Id);
        DbContext.ChangeTracker.Clear();

        // Act
        await service.UpdateAsync(
            uploadConfig.Id,
            uploadConfig.Name,
            UploadTarget.ForHosterAccountPool(hosterAccountPool.Id),
            seed.ArchiveConfigId,
            false,
            CancellationToken.None
        );

        // Assert
        var result = await DbContext.UploadConfigs.SingleAsync();
        var hosterRegistrationIdsByUploadId = await DbContext
            .Uploads.Select(upload => new { upload.Id, upload.HosterRegistrationId })
            .ToDictionaryAsync(upload => upload.Id, upload => upload.HosterRegistrationId);

        result.HosterRegistrationId.ShouldBeNull();
        result.HosterAccountPoolId.ShouldBe(hosterAccountPool.Id);
        hosterRegistrationIdsByUploadId.ShouldBe(
            new Dictionary<int, int?>
            {
                [waitingUpload.Id] = null,
                [pendingUploadWithUploadedFile.Id] = seed.HosterRegistrationId,
                [completedUpload.Id] = seed.HosterRegistrationId,
            },
            ignoreOrder: true
        );
    }

    [Test]
    public async Task GetUploadConfigsAsync_UploadsWithDownloadCounts_AggregatesIndividualAndCompleteDownloads()
    {
        // Arrange
        var seed = await AddUploadConfigDependenciesAsync();
        var uploadConfig = await AddUploadConfigAsync(seed);
        var archive = await AddArchiveAsync(seed.ArchiveConfigId);

        await AddUploadWithFilesAsync(uploadConfig, archive.Id, [10, 12, 8]);
        await AddUploadWithFilesAsync(uploadConfig, archive.Id, [4, 6, null]);

        var readRepository = new UploadConfigReadRepository(DbContext);

        // Act
        var result = await readRepository.GetUploadConfigsAsync(
            seed.ReleaseId,
            CancellationToken.None
        );

        // Assert
        var config = result.ShouldHaveSingleItem();
        config.TotalIndividualDownloads.ShouldBe(40);
        config.TotalCompleteDownloads.ShouldBe(15d);
    }

    [Test]
    public async Task GetUploadConfigsAsync_NoDownloadCountsKnown_LeavesAggregatesNull()
    {
        // Arrange
        var seed = await AddUploadConfigDependenciesAsync();
        var uploadConfig = await AddUploadConfigAsync(seed);
        var archive = await AddArchiveAsync(seed.ArchiveConfigId);

        await AddUploadWithFilesAsync(uploadConfig, archive.Id, [null, null]);

        var readRepository = new UploadConfigReadRepository(DbContext);

        // Act
        var result = await readRepository.GetUploadConfigsAsync(
            seed.ReleaseId,
            CancellationToken.None
        );

        // Assert
        var config = result.ShouldHaveSingleItem();
        config.TotalIndividualDownloads.ShouldBeNull();
        config.TotalCompleteDownloads.ShouldBeNull();
    }

    private async Task<HosterAccountPool> AddHosterAccountPoolAsync(UploadConfigSeed seed)
    {
        var hosterAccountPool = new HosterAccountPool
        {
            Name = "First pool",
            HosterClassName = "TestHoster",
            Members =
            [
                new HosterAccountPoolMember
                {
                    HosterRegistrationId = seed.HosterRegistrationId,
                    Priority = 1,
                },
            ],
        };

        DbContext.HosterAccountPools.Add(hosterAccountPool);
        await DbContext.SaveChangesAsync();

        return hosterAccountPool;
    }

    private async Task<Archive> AddArchiveAsync(int archiveConfigId)
    {
        var archive = new Archive
        {
            ArchiveConfigId = archiveConfigId,
            ArchiveFolderPath = "/tmp/archive",
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow,
        };

        DbContext.Archives.Add(archive);
        await DbContext.SaveChangesAsync();

        return archive;
    }

    private async Task<Upload> AddUploadAsync(
        UploadConfig uploadConfig,
        UploadState uploadState,
        int? archiveIdOfUploadedFile = null
    )
    {
        var upload = new Upload
        {
            UploadConfigId = uploadConfig.Id,
            HosterRegistrationId = uploadConfig.HosterRegistrationId,
            ArchiveId = archiveIdOfUploadedFile,
            UploadState = uploadState,
            OnlineState = OnlineState.Unknown,
            CreatedAt = DateTime.UtcNow,
            UploadedFiles = [],
        };

        if (archiveIdOfUploadedFile is not null)
        {
            upload.UploadedFiles.Add(
                new UploadedFile
                {
                    ArchiveFile = new ArchiveFile
                    {
                        ArchiveId = archiveIdOfUploadedFile.Value,
                        FullFileName = "archive.part1.rar",
                    },
                    HosterFileLink = $"https://hoster.test/file/{Guid.NewGuid()}",
                    OnlineState = OnlineState.Online,
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }

        DbContext.Uploads.Add(upload);
        await DbContext.SaveChangesAsync();

        return upload;
    }

    private async Task AddUploadWithFilesAsync(
        UploadConfig uploadConfig,
        int archiveId,
        IReadOnlyList<int?> downloadCounts
    )
    {
        var upload = new Upload
        {
            UploadConfigId = uploadConfig.Id,
            HosterRegistrationId = uploadConfig.HosterRegistrationId,
            ArchiveId = archiveId,
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Online,
            CreatedAt = DateTime.UtcNow,
            UploadedFiles = [],
        };

        foreach (var (downloadCount, index) in downloadCounts.Select((value, i) => (value, i)))
        {
            upload.UploadedFiles.Add(
                new UploadedFile
                {
                    ArchiveFile = new ArchiveFile
                    {
                        ArchiveId = archiveId,
                        FullFileName = $"archive.part{index}.rar",
                    },
                    HosterFileLink = $"https://hoster.test/file/{Guid.NewGuid()}",
                    OnlineState = OnlineState.Online,
                    DownloadCount = downloadCount,
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }

        DbContext.Uploads.Add(upload);
        await DbContext.SaveChangesAsync();
    }

    private async Task<UploadConfig> AddUploadConfigAsync(
        UploadConfigSeed seed,
        string name = "Default upload",
        bool premiumOnlyDownload = false
    )
    {
        var uploadConfig = new UploadConfig
        {
            UploadSource = UploadSource.ArchiveFiles,
            ReleaseId = seed.ReleaseId,
            ArchiveConfigId = seed.ArchiveConfigId,
            HosterRegistrationId = seed.HosterRegistrationId,
            Name = name,
            PremiumOnlyDownload = premiumOnlyDownload,
        };

        DbContext.UploadConfigs.Add(uploadConfig);
        await DbContext.SaveChangesAsync();

        return uploadConfig;
    }

    private async Task<UploadConfigSeed> AddUploadConfigDependenciesAsync(
        string suffix = "First",
        bool hosterIsActive = true,
        ReleaseType releaseType = ReleaseType.Managed,
        ReleaseContentType releaseContentType = ReleaseContentType.Movie,
        string hosterClassName = "TestHoster"
    )
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = $"{suffix} group",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };
        var release = new Release
        {
            Name = $"Bearcat.Release.{suffix}",
            ReleaseType = releaseType,
            ReleaseContentType = releaseContentType,
            ReleaseFolderPath = $"/tmp/release-{suffix}",
            ReleaseGroup = releaseGroup,
        };
        var archiveConfig = new ArchiveConfig
        {
            Release = release,
            Name = $"{suffix} archive",
            ArchiveFilesBasePath = $"/tmp/archive-{suffix}",
            ArchiverName = "zip",
            ArchiveNamePrefix = "bearcat-release",
            ArchivePassword = "secret",
            ArchiveFileSizeMb = 512,
        };
        var hosterRegistration = new HosterRegistration
        {
            Name = $"{suffix} hoster",
            SerializedConfig = "{}",
            HosterClassName = hosterClassName,
            IsActive = hosterIsActive,
        };

        DbContext.ArchiveConfigs.Add(archiveConfig);
        DbContext.HosterRegistrations.Add(hosterRegistration);
        await DbContext.SaveChangesAsync();

        return new UploadConfigSeed(release.Id, archiveConfig.Id, hosterRegistration.Id);
    }

    private sealed record UploadConfigSeed(
        int ReleaseId,
        int ArchiveConfigId,
        int HosterRegistrationId
    );
}
