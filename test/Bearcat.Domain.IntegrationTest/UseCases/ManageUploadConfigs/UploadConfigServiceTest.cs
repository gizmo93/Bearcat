using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploadConfigs;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageUploadConfigs;

public class UploadConfigServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private UploadConfigService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        service = new UploadConfigService(new UploadConfigWriteRepository(dbContext));
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
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
        var uploadConfig = await dbContext.UploadConfigs.SingleAsync();

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
        var result = await dbContext.UploadConfigs.SingleAsync();

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
        var result = await dbContext.UploadConfigs.AnyAsync();

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
        var readRepository = new UploadConfigReadRepository(dbContext);

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

        var readRepository = new UploadConfigReadRepository(dbContext);

        // Act
        var result = await readRepository.GetUploadConfigsAsync(
            seed.ReleaseId,
            CancellationToken.None
        );

        // Assert
        var config = result.ShouldHaveSingleItem();
        config.TotalIndividualDownloads.ShouldBe(40);
        config.TotalCompleteDownloads.ShouldBe(15);
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

        dbContext.Archives.Add(archive);
        await dbContext.SaveChangesAsync();

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

        dbContext.Uploads.Add(upload);
        await dbContext.SaveChangesAsync();
    }

    private async Task<UploadConfig> AddUploadConfigAsync(UploadConfigSeed seed)
    {
        var uploadConfig = new UploadConfig
        {
            ReleaseId = seed.ReleaseId,
            ArchiveConfigId = seed.ArchiveConfigId,
            HosterRegistrationId = seed.HosterRegistrationId,
            Name = "Default upload",
        };

        dbContext.UploadConfigs.Add(uploadConfig);
        await dbContext.SaveChangesAsync();

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

        dbContext.ArchiveConfigs.Add(archiveConfig);
        dbContext.HosterRegistrations.Add(hosterRegistration);
        await dbContext.SaveChangesAsync();

        return new UploadConfigSeed(release.Id, archiveConfig.Id, hosterRegistration.Id);
    }

    private sealed record UploadConfigSeed(
        int ReleaseId,
        int ArchiveConfigId,
        int HosterRegistrationId
    );
}
