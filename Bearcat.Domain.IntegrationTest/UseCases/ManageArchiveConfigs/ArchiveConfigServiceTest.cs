using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchiveConfigs;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageArchiveConfigs;

public class ArchiveConfigServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private ArchiveConfigService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();

        var repository = new ArchiveConfigWriteRepository(dbContext);
        service = new ArchiveConfigService(repository);
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task CreateAsync_ValidArchiveConfig_PersistsArchiveConfigAndReturnsId()
    {
        // Arrange
        var releaseId = await AddReleaseAsync();

        // Act
        var result = await service.CreateAsync(
            releaseId,
            "/data/releases",
            "zip",
            "bearcat-release",
            "secret",
            "Main archive",
            512
        );

        // Assert
        result.ShouldBeGreaterThan(0);

        var archiveConfig = await dbContext.ArchiveConfigs.SingleAsync();
        archiveConfig.ShouldNotBeNull();
        archiveConfig.Id.ShouldBe(result);
        archiveConfig.ReleaseId.ShouldBe(releaseId);
        archiveConfig.ArchiveFilesBasePath.ShouldBe("/data/releases");
        archiveConfig.ArchiverName.ShouldBe("zip");
        archiveConfig.ArchiveNamePrefix.ShouldBe("bearcat-release");
        archiveConfig.ArchivePassword.ShouldBe("secret");
        archiveConfig.Name.ShouldBe("Main archive");
        archiveConfig.ArchiveFileSizeMb.ShouldBe(512);
    }

    [Test]
    public async Task CreateAsync_ArchiveFileSizeIsNull_PersistsArchiveConfigWithZeroFileSize()
    {
        // Arrange
        var releaseId = await AddReleaseAsync();

        // Act
        var result = await service.CreateAsync(
            releaseId,
            "/data/releases",
            "zip",
            "bearcat-release",
            null,
            "Main archive",
            null
        );

        // Assert
        result.ShouldBeGreaterThan(0);

        var archiveConfig = await dbContext.ArchiveConfigs.SingleAsync();
        archiveConfig.ShouldNotBeNull();
        archiveConfig.ArchivePassword.ShouldBeNull();
        archiveConfig.ArchiveFileSizeMb.ShouldBe(0);
    }

    [Test]
    public async Task DeleteAsync_ArchiveConfigExists_RemovesArchiveConfig()
    {
        // Arrange
        var archiveConfig = await AddArchiveConfigAsync();

        // Act
        await service.DeleteAsync(archiveConfig.Id);

        // Assert
        var result = await dbContext.ArchiveConfigs.AnyAsync();

        result.ShouldBeFalse();
    }

    [Test]
    public async Task DeleteAsync_ArchiveConfigDoesNotExist_ThrowsInvalidOperationException()
    {
        // Arrange
        var archiveConfigId = 404;

        // Act
        var result = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.DeleteAsync(archiveConfigId)
        );

        // Assert
        result.ShouldNotBeNull();
        result.Message.ShouldBe($"ArchiveConfig with ID {archiveConfigId} not found");
    }

    [Test]
    public async Task UpdateAsync_ArchiveConfigExists_UpdatesArchiveConfig()
    {
        // Arrange
        var archiveConfig = await AddArchiveConfigAsync();

        // Act
        await service.UpdateAsync(
            archiveConfig.Id,
            "/new/releases",
            "updated-prefix",
            "new-secret",
            "Updated archive",
            256
        );

        // Assert
        var result = await dbContext.ArchiveConfigs.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(archiveConfig.Id);
        result.ReleaseId.ShouldBe(archiveConfig.ReleaseId);
        result.ArchiverName.ShouldBe("zip");
        result.ArchiveFilesBasePath.ShouldBe("/new/releases");
        result.ArchiveNamePrefix.ShouldBe("updated-prefix");
        result.ArchivePassword.ShouldBe("new-secret");
        result.Name.ShouldBe("Updated archive");
        result.ArchiveFileSizeMb.ShouldBe(256);
    }

    [Test]
    public async Task UpdateAsync_ArchiveFileSizeIsNull_UpdatesArchiveConfigWithZeroFileSize()
    {
        // Arrange
        var archiveConfig = await AddArchiveConfigAsync();

        // Act
        await service.UpdateAsync(
            archiveConfig.Id,
            "/new/releases",
            "updated-prefix",
            null,
            "Updated archive",
            null
        );

        // Assert
        var result = await dbContext.ArchiveConfigs.SingleAsync();

        result.ShouldNotBeNull();
        result.ArchivePassword.ShouldBeNull();
        result.ArchiveFileSizeMb.ShouldBe(0);
    }

    [Test]
    public async Task UpdateAsync_ArchiveConfigDoesNotExist_ThrowsInvalidOperationException()
    {
        // Arrange
        var archiveConfigId = 404;

        // Act
        var result = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.UpdateAsync(
                archiveConfigId,
                "/new/releases",
                "updated-prefix",
                "new-secret",
                "Updated archive",
                256
            )
        );

        // Assert
        result.ShouldNotBeNull();
        result.Message.ShouldBe($"ArchiveConfig with ID {archiveConfigId} not found");
    }

    private async Task<ArchiveConfig> AddArchiveConfigAsync()
    {
        var releaseId = await AddReleaseAsync();
        var archiveConfig = new ArchiveConfig
        {
            ReleaseId = releaseId,
            ArchiveFilesBasePath = "/data/releases",
            ArchiverName = "zip",
            ArchiveNamePrefix = "bearcat-release",
            ArchivePassword = "secret",
            Name = "Main archive",
            ArchiveFileSizeMb = 512,
        };

        dbContext.ArchiveConfigs.Add(archiveConfig);
        await dbContext.SaveChangesAsync();

        return archiveConfig;
    }

    private async Task<int> AddReleaseAsync()
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = "Managed releases",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };
        var release = new Release
        {
            Name = "Bearcat.Release.001",
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/data/releases/Bearcat.Release.001",
            ReleaseGroup = releaseGroup,
        };

        dbContext.Releases.Add(release);
        await dbContext.SaveChangesAsync();

        return release.Id;
    }
}
