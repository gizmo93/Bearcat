using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploadConfigs;
using Bearcat.Domain.UseCases.ManageUploadConfigs.ReadModels;
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
        service = new UploadConfigService(new UploadConfigWriteRepository(DbContext));
    }

    [Test]
    public async Task CreateAsync_ValidUploadConfig_PersistsUploadConfigAndReturnsId()
    {
        // Arrange
        var seed = await AddUploadConfigDependenciesAsync();

        // Act
        var result = await service.CreateAsync(
            seed.ReleaseId,
            "Default upload",
            seed.HosterRegistrationId,
            seed.ArchiveConfigId,
            true,
            CancellationToken.None
        );

        // Assert
        var uploadConfig = await DbContext.UploadConfigs.SingleAsync();

        result.ShouldBeGreaterThan(0);
        uploadConfig.ShouldNotBeNull();
        uploadConfig.Id.ShouldBe(result);
        uploadConfig.ReleaseId.ShouldBe(seed.ReleaseId);
        uploadConfig.HosterRegistrationId.ShouldBe(seed.HosterRegistrationId);
        uploadConfig.ArchiveConfigId.ShouldBe(seed.ArchiveConfigId);
        uploadConfig.Name.ShouldBe("Default upload");
        uploadConfig.PremiumOnlyDownload.ShouldBeTrue();
    }

    [Test]
    public async Task UpdateAsync_UploadConfigExists_UpdatesUploadConfig()
    {
        // Arrange
        var firstSeed = await AddUploadConfigDependenciesAsync();
        var uploadConfig = await AddUploadConfigAsync(firstSeed);
        var secondSeed = await AddUploadConfigDependenciesAsync("Second");

        // Act
        await service.UpdateAsync(
            uploadConfig.Id,
            "Updated upload",
            secondSeed.HosterRegistrationId,
            secondSeed.ArchiveConfigId,
            true,
            CancellationToken.None
        );

        // Assert
        var result = await DbContext.UploadConfigs.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(uploadConfig.Id);
        result.Name.ShouldBe("Updated upload");
        result.HosterRegistrationId.ShouldBe(secondSeed.HosterRegistrationId);
        result.ArchiveConfigId.ShouldBe(secondSeed.ArchiveConfigId);
        result.PremiumOnlyDownload.ShouldBeTrue();
    }

    [Test]
    public async Task DeleteAsync_UploadConfigExists_RemovesUploadConfig()
    {
        // Arrange
        var seed = await AddUploadConfigDependenciesAsync();
        var uploadConfig = await AddUploadConfigAsync(seed);

        // Act
        await service.DeleteAsync(uploadConfig.Id, CancellationToken.None);

        // Assert
        var result = await DbContext.UploadConfigs.AnyAsync();

        result.ShouldBeFalse();
    }

    [Test]
    public async Task GetHosterRegistrationOptionsAsync_InactiveHosterRegistration_ExcludesInactiveOption()
    {
        // Arrange
        var activeSeed = await AddUploadConfigDependenciesAsync("Active", hosterIsActive: true);
        var inactiveSeed = await AddUploadConfigDependenciesAsync(
            "Inactive",
            hosterIsActive: false
        );
        var readRepository = new UploadConfigReadRepository(DbContext);

        // Act
        var result = await readRepository.GetHosterRegistrationOptionsAsync(CancellationToken.None);

        // Assert
        result.ShouldContainKey(activeSeed.HosterRegistrationId);
        result.ShouldNotContainKey(inactiveSeed.HosterRegistrationId);
    }

    [Test]
    public async Task GetUploadConfigsAsync_UploadsWithDownloadCounts_AggregatesIndividualAndCompleteDownloads()
    {
        // Arrange
        var seed = await AddUploadConfigDependenciesAsync();
        var uploadConfig = await AddUploadConfigAsync(seed);
        var archive = await AddArchiveAsync(seed.ArchiveConfigId);

        await AddUploadWithFilesAsync(uploadConfig.Id, archive.Id, [10, 12, 8]);
        await AddUploadWithFilesAsync(uploadConfig.Id, archive.Id, [4, 6, null]);

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

        await AddUploadWithFilesAsync(uploadConfig.Id, archive.Id, [null, null]);

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

    [Test]
    public async Task GetReadModelByIdAsync_SeveralUploadConfigsWithDownloads_ReturnsRequestedConfigWithItsAggregates()
    {
        // Arrange
        var seed = await AddUploadConfigDependenciesAsync();
        var firstUploadConfig = await AddUploadConfigAsync(seed);
        var secondUploadConfig = await AddUploadConfigAsync(
            seed,
            "Mirror upload",
            premiumOnlyDownload: true
        );
        var archive = await AddArchiveAsync(seed.ArchiveConfigId);

        await AddUploadWithFilesAsync(firstUploadConfig.Id, archive.Id, [100, 200]);
        await AddUploadWithFilesAsync(secondUploadConfig.Id, archive.Id, [7, 9, null]);
        await AddUploadWithFilesAsync(secondUploadConfig.Id, archive.Id, [3]);
        DbContext.ChangeTracker.Clear();

        var readRepository = new UploadConfigReadRepository(DbContext);

        // Act
        var result = await readRepository.GetReadModelByIdAsync(
            secondUploadConfig.Id,
            CancellationToken.None
        );

        // Assert
        result.UploadConfigId.ShouldBe(secondUploadConfig.Id);
        result.Name.ShouldBe("Mirror upload");
        result.HosterRegistrationName.ShouldBe("First hoster");
        result.HosterRegistrationId.ShouldBe(seed.HosterRegistrationId);
        result.ArchiveConfigId.ShouldBe(seed.ArchiveConfigId);
        result.ArchiveConfigName.ShouldBe("First archive");
        result.ReleaseName.ShouldBe("Bearcat.Release.First");
        result.PremiumOnlyDownload.ShouldBeTrue();
        result.TotalIndividualDownloads.ShouldBe(19);
        result.TotalCompleteDownloads.ShouldBe(11d);
    }

    [Test]
    public async Task GetArchiveConfigOptionsAsync_SeveralReleases_ReturnsArchiveConfigsOfRelease()
    {
        // Arrange
        var firstSeed = await AddUploadConfigDependenciesAsync();
        await AddUploadConfigDependenciesAsync("Second");
        var additionalArchiveConfig = new ArchiveConfig
        {
            ReleaseId = firstSeed.ReleaseId,
            Name = "Additional archive",
            ArchiveFilesBasePath = "/tmp/archive-additional",
            ArchiverName = "zip",
            ArchiveFileSizeMb = 1024,
        };
        DbContext.ArchiveConfigs.Add(additionalArchiveConfig);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var readRepository = new UploadConfigReadRepository(DbContext);

        // Act
        var result = await readRepository.GetArchiveConfigOptionsAsync(
            firstSeed.ReleaseId,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(
            [
                new ArchiveConfigOptionReadModel(firstSeed.ArchiveConfigId, "First archive", 512),
                new ArchiveConfigOptionReadModel(
                    additionalArchiveConfig.Id,
                    "Additional archive",
                    1024
                ),
            ],
            ignoreOrder: true
        );
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

    private async Task AddUploadWithFilesAsync(
        int uploadConfigId,
        int archiveId,
        IReadOnlyList<int?> downloadCounts
    )
    {
        var upload = new Upload
        {
            UploadConfigId = uploadConfigId,
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
        bool hosterIsActive = true
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
            ReleaseType = ReleaseType.Managed,
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
            HosterClassName = "TestHoster",
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
