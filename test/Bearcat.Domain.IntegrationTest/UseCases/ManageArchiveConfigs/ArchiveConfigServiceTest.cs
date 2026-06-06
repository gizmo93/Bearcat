using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchiveConfigs;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageArchiveConfigs;

public class ArchiveConfigServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private ArchiveConfigService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        var archiverFactory = new Mock<IArchiverFactory>();
        archiverFactory
            .Setup(f => f.GetArchivers())
            .Returns([new ArchiverDto("RAR", "RarArchiver", ".rar")]);

        var repository = new ArchiveConfigWriteRepository(dbContext);
        service = new ArchiveConfigService(
            repository,
            archiverFactory.Object,
            CreateTimeProvider()
        );
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
    public async Task DeleteAsync_UnmanagedReleaseArchiveConfig_ThrowsInvalidOperationException()
    {
        // Arrange
        var archiveConfig = await AddArchiveConfigAsync(ReleaseType.Unmanaged);

        // Act
        var result = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.DeleteAsync(archiveConfig.Id)
        );

        // Assert
        result.Message.ShouldBe("Archive configs for unmanaged releases cannot be changed.");
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

    [Test]
    public async Task UpdateAsync_UnmanagedReleaseArchiveConfig_ThrowsInvalidOperationException()
    {
        // Arrange
        var archiveConfig = await AddArchiveConfigAsync(ReleaseType.Unmanaged);

        // Act
        var result = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.UpdateAsync(
                archiveConfig.Id,
                "/new/releases",
                "updated-prefix",
                "new-secret",
                "Updated archive",
                256
            )
        );

        // Assert
        result.Message.ShouldBe("Archive configs for unmanaged releases cannot be changed.");
    }

    [Test]
    public async Task RefreshUnmanagedArchiveAsync_AllArchiveFilesExist_DoesNotCreateNewArchive()
    {
        // Arrange
        var releaseFolderPath = CreateReleaseFolderWithFiles(
            "Bearcat.Release.Unmanaged.part1.rar",
            "Bearcat.Release.Unmanaged.part2.rar"
        );
        var archiveConfig = await AddUnmanagedArchiveConfigAsync(
            releaseFolderPath,
            "Bearcat.Release.Unmanaged.part1.rar",
            "Bearcat.Release.Unmanaged.part2.rar"
        );

        // Act
        await service.RefreshUnmanagedArchiveAsync(archiveConfig.Id);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
            .ArchiveConfigs.AsSplitQuery()
            .Include(c => c.Archives)
                .ThenInclude(a => a.ArchiveFiles)
            .SingleAsync(c => c.Id == archiveConfig.Id);
        var archive = result.Archives.Single();

        result.ArchiveFilesBasePath.ShouldBe(releaseFolderPath);
        archive.ArchiveState.ShouldBe(ArchiveState.Created);
        archive
            .ArchiveFiles.Select(file => file.FullFileName)
            .ShouldBe([
                Path.Combine(releaseFolderPath, "Bearcat.Release.Unmanaged.part1.rar"),
                Path.Combine(releaseFolderPath, "Bearcat.Release.Unmanaged.part2.rar"),
            ]);
    }

    [Test]
    public async Task RefreshUnmanagedArchiveAsync_ArchiveFilesChanged_CreatesNewArchive()
    {
        // Arrange
        var releaseFolderPath = CreateReleaseFolderWithFiles(
            "Bearcat.Release.Unmanaged.part1.rar",
            "Bearcat.Release.Unmanaged.part2.rar"
        );
        var archiveConfig = await AddUnmanagedArchiveConfigAsync(
            releaseFolderPath,
            "Bearcat.Release.Unmanaged.part1.rar",
            "Bearcat.Release.Unmanaged.part2.rar"
        );
        File.Delete(Path.Combine(releaseFolderPath, "Bearcat.Release.Unmanaged.part1.rar"));
        File.Delete(Path.Combine(releaseFolderPath, "Bearcat.Release.Unmanaged.part2.rar"));
        File.WriteAllText(Path.Combine(releaseFolderPath, "Bearcat.Release.Unmanaged.rar"), "new");

        // Act
        await service.RefreshUnmanagedArchiveAsync(archiveConfig.Id);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
            .ArchiveConfigs.AsSplitQuery()
            .Include(c => c.Archives)
                .ThenInclude(a => a.ArchiveFiles)
            .SingleAsync(c => c.Id == archiveConfig.Id);
        var archives = result.Archives.OrderBy(a => a.Id).ToList();

        archives.Count.ShouldBe(2);
        archives[0].ArchiveState.ShouldBe(ArchiveState.Deleted);
        archives[1].ArchiveState.ShouldBe(ArchiveState.Created);
        archives[1]
            .ArchiveFiles.Single()
            .FullFileName.ShouldBe(
                Path.Combine(releaseFolderPath, "Bearcat.Release.Unmanaged.rar")
            );
    }

    [Test]
    public async Task RefreshUnmanagedArchiveAsync_TargetHasAdditionalArchiveFiles_CreatesNewArchive()
    {
        // Arrange
        var releaseFolderPath = CreateReleaseFolderWithFiles(
            "Bearcat.Release.Unmanaged.part1.rar",
            "Bearcat.Release.Unmanaged.part2.rar"
        );
        var archiveConfig = await AddUnmanagedArchiveConfigAsync(
            releaseFolderPath,
            "Bearcat.Release.Unmanaged.part1.rar"
        );

        // Act
        await service.RefreshUnmanagedArchiveAsync(archiveConfig.Id);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
            .ArchiveConfigs.AsSplitQuery()
            .Include(c => c.Archives)
                .ThenInclude(a => a.ArchiveFiles)
            .SingleAsync(c => c.Id == archiveConfig.Id);
        var archives = result.Archives.OrderBy(a => a.Id).ToList();

        archives.Count.ShouldBe(2);
        archives[0].ArchiveState.ShouldBe(ArchiveState.Deleted);
        archives[1].ArchiveState.ShouldBe(ArchiveState.Created);
        archives[1]
            .ArchiveFiles.Select(file => file.FullFileName)
            .ShouldBe([
                Path.Combine(releaseFolderPath, "Bearcat.Release.Unmanaged.part1.rar"),
                Path.Combine(releaseFolderPath, "Bearcat.Release.Unmanaged.part2.rar"),
            ]);
    }

    [Test]
    public async Task RefreshUnmanagedArchiveAsync_NoMatchingArchiveFiles_ThrowsInvalidOperationException()
    {
        // Arrange
        var releaseFolderPath = CreateReleaseFolderWithFiles("Bearcat.Release.Unmanaged.part1.rar");
        var archiveConfig = await AddUnmanagedArchiveConfigAsync(
            releaseFolderPath,
            "Bearcat.Release.Unmanaged.part1.rar"
        );
        File.Delete(Path.Combine(releaseFolderPath, "Bearcat.Release.Unmanaged.part1.rar"));
        File.WriteAllText(Path.Combine(releaseFolderPath, "Bearcat.Release.Unmanaged.zip"), "zip");

        // Act
        var result = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.RefreshUnmanagedArchiveAsync(archiveConfig.Id)
        );

        // Assert
        result.Message.ShouldBe(
            $"Release folder path {releaseFolderPath} does not contain archive files for archiver RAR."
        );
    }

    [Test]
    public async Task RefreshUnmanagedArchiveAsync_ManagedReleaseArchiveConfig_ThrowsInvalidOperationException()
    {
        // Arrange
        var archiveConfig = await AddArchiveConfigAsync();

        // Act
        var result = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.RefreshUnmanagedArchiveAsync(archiveConfig.Id)
        );

        // Assert
        result.Message.ShouldBe("Archives can only be refreshed for unmanaged releases.");
    }

    private async Task<ArchiveConfig> AddArchiveConfigAsync(
        ReleaseType releaseType = ReleaseType.Managed
    )
    {
        var releaseId = await AddReleaseAsync(releaseType);
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

    private async Task<ArchiveConfig> AddUnmanagedArchiveConfigAsync(
        string releaseFolderPath,
        params string[] archiveFileNames
    )
    {
        var releaseId = await AddReleaseAsync(ReleaseType.Unmanaged, releaseFolderPath);
        var archiveConfig = new ArchiveConfig
        {
            ReleaseId = releaseId,
            ArchiveFilesBasePath = releaseFolderPath,
            ArchiverName = "RarArchiver",
            ArchiveNamePrefix = null,
            ArchivePassword = null,
            Name = "RAR",
            ArchiveFileSizeMb = 0,
            Archives =
            [
                new Archive
                {
                    ArchiveFolderPath = releaseFolderPath,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                    ArchiveState = ArchiveState.Created,
                    ArchiveFileSizeMb = 0,
                    ArchiveFiles = archiveFileNames
                        .Select(fileName => new ArchiveFile
                        {
                            FullFileName = Path.Combine(releaseFolderPath, fileName),
                        })
                        .ToList(),
                    Uploads = [],
                    ErrorMessages = [],
                    Notifications = [],
                },
            ],
        };

        dbContext.ArchiveConfigs.Add(archiveConfig);
        await dbContext.SaveChangesAsync();

        return archiveConfig;
    }

    private async Task<int> AddReleaseAsync(ReleaseType releaseType = ReleaseType.Managed)
    {
        return await AddReleaseAsync(releaseType, "/data/releases/Bearcat.Release.001");
    }

    private async Task<int> AddReleaseAsync(ReleaseType releaseType, string releaseFolderPath)
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
            ReleaseType = releaseType,
            ReleaseFolderPath = releaseFolderPath,
            ReleaseGroup = releaseGroup,
        };

        dbContext.Releases.Add(release);
        await dbContext.SaveChangesAsync();

        return release.Id;
    }

    private static string CreateReleaseFolderWithFiles(params string[] fileNames)
    {
        var releaseFolderPath = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "archive-config-service-test",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(releaseFolderPath);

        foreach (var fileName in fileNames)
        {
            File.WriteAllText(Path.Combine(releaseFolderPath, fileName), fileName);
        }

        return releaseFolderPath;
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }
}
