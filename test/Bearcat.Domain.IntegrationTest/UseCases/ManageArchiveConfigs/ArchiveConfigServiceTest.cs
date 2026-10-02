using Bearcat.Domain.Entities;
using Bearcat.Domain.IntegrationTest.Shared.UnmanagedReleases;
using Bearcat.Domain.Shared.UnmanagedReleases;
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

public class ArchiveConfigServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private ArchiveConfigService service = null!;

    [SetUp]
    public void Setup()
    {
        var repository = new ArchiveConfigWriteRepository(DbContext);
        service = new ArchiveConfigService(
            repository,
            new UnmanagedReleaseArchiveInitializationService(new RealArchiverFactory()),
            CreateTimeProvider()
        );
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
            512,
            []
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var archiveConfig = await DbContext.ArchiveConfigs.SingleAsync();
        archiveConfig.ShouldNotBeNull();
        archiveConfig.Id.ShouldBe(result.ArchiveConfigId!.Value);
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
            null,
            []
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var archiveConfig = await DbContext.ArchiveConfigs.SingleAsync();
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
        var result = await DbContext.ArchiveConfigs.AnyAsync();

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
            256,
            []
        );

        // Assert
        var result = await DbContext.ArchiveConfigs.SingleAsync();

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
            null,
            []
        );

        // Assert
        var result = await DbContext.ArchiveConfigs.SingleAsync();

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
                256,
                []
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
                256,
                []
            )
        );

        // Assert
        result.Message.ShouldBe("Archive configs for unmanaged releases cannot be changed.");
    }

    [Test]
    public async Task CreateAsync_WithAdditionalArchiveContentIds_PersistsAssignments()
    {
        // Arrange
        var releaseId = await AddReleaseAsync();
        var additionalArchiveContents = await AddAdditionalArchiveContentsAsync();

        // Act
        var result = await service.CreateAsync(
            releaseId,
            "/data/releases",
            "zip",
            "bearcat-release",
            null,
            "Main archive",
            512,
            additionalArchiveContents.Select(content => content.Id).ToList()
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var archiveConfig = await CreateDbContext()
            .ArchiveConfigs.Include(config => config.AdditionalArchiveContents)
            .SingleAsync(config => config.Id == result.ArchiveConfigId);

        archiveConfig
            .AdditionalArchiveContents.Select(content => content.Id)
            .Order()
            .ShouldBe(additionalArchiveContents.Select(content => content.Id).Order());
    }

    [Test]
    public async Task CreateAsync_ContentsWithSameEntryName_ReturnsEntryNameCollisionAndDoesNotPersist()
    {
        // Arrange
        var releaseId = await AddReleaseAsync();
        var collidingAdditionalArchiveContents =
            await AddAdditionalArchiveContentsWithSameEntryNameAsync();

        // Act
        var result = await service.CreateAsync(
            releaseId,
            "/data/releases",
            "zip",
            "bearcat-release",
            null,
            "Main archive",
            512,
            collidingAdditionalArchiveContents.Select(content => content.Id).ToList()
        );

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ArchiveConfigId.ShouldBeNull();
        result.AdditionalArchiveContentEntryNameCollisions.ShouldHaveSingleItem();
        result
            .AdditionalArchiveContentEntryNameCollisions[0]
            .AdditionalArchiveContentNames.ShouldBe(["Premium ad folder", "Premium ad text"]);
        (await CreateDbContext().ArchiveConfigs.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task UpdateAsync_DifferentAdditionalArchiveContentIds_ReplacesAssignmentsAndKeepsContents()
    {
        // Arrange
        var additionalArchiveContents = await AddAdditionalArchiveContentsAsync();
        var archiveConfig = await AddArchiveConfigAsync();
        archiveConfig.AdditionalArchiveContents = [additionalArchiveContents[0]];
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await service.UpdateAsync(
            archiveConfig.Id,
            "/data/releases",
            "bearcat-release",
            null,
            "Main archive",
            512,
            [additionalArchiveContents[1].Id]
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var verificationDbContext = CreateDbContext();
        var updatedArchiveConfig = await verificationDbContext
            .ArchiveConfigs.Include(config => config.AdditionalArchiveContents)
            .SingleAsync(config => config.Id == archiveConfig.Id);

        updatedArchiveConfig
            .AdditionalArchiveContents.Select(content => content.Id)
            .ShouldBe([additionalArchiveContents[1].Id]);
        (await verificationDbContext.AdditionalArchiveContents.CountAsync()).ShouldBe(
            additionalArchiveContents.Count
        );
    }

    [Test]
    public async Task UpdateAsync_EmptyAdditionalArchiveContentIds_RemovesAssignmentsAndKeepsContents()
    {
        // Arrange
        var additionalArchiveContents = await AddAdditionalArchiveContentsAsync();
        var archiveConfig = await AddArchiveConfigAsync();
        archiveConfig.AdditionalArchiveContents = additionalArchiveContents;
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await service.UpdateAsync(
            archiveConfig.Id,
            "/data/releases",
            "bearcat-release",
            null,
            "Main archive",
            512,
            []
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var verificationDbContext = CreateDbContext();
        var updatedArchiveConfig = await verificationDbContext
            .ArchiveConfigs.Include(config => config.AdditionalArchiveContents)
            .SingleAsync(config => config.Id == archiveConfig.Id);

        updatedArchiveConfig.AdditionalArchiveContents.ShouldBeEmpty();
        (await verificationDbContext.AdditionalArchiveContents.CountAsync()).ShouldBe(
            additionalArchiveContents.Count
        );
    }

    [Test]
    public async Task UpdateAsync_ContentsWithSameEntryName_ReturnsEntryNameCollisionAndKeepsArchiveConfigUnchanged()
    {
        // Arrange
        var archiveConfig = await AddArchiveConfigAsync();
        var collidingAdditionalArchiveContents =
            await AddAdditionalArchiveContentsWithSameEntryNameAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await service.UpdateAsync(
            archiveConfig.Id,
            "/new/releases",
            "updated-prefix",
            null,
            "Updated archive",
            256,
            collidingAdditionalArchiveContents.Select(content => content.Id).ToList()
        );

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.AdditionalArchiveContentEntryNameCollisions.ShouldHaveSingleItem();
        result
            .AdditionalArchiveContentEntryNameCollisions[0]
            .EntryName.ShouldBe("premium", StringCompareShould.IgnoreCase);

        var unchangedArchiveConfig = await CreateDbContext()
            .ArchiveConfigs.Include(config => config.AdditionalArchiveContents)
            .SingleAsync(config => config.Id == archiveConfig.Id);

        unchangedArchiveConfig.Name.ShouldBe("Main archive");
        unchangedArchiveConfig.AdditionalArchiveContents.ShouldBeEmpty();
    }

    [Test]
    public async Task SetArchiveFolderAsync_AllArchiveFilesExist_RelocatesArchive()
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
        var changeResult = await service.SetArchiveFolderAsync(
            archiveConfig.Id,
            releaseFolderPath,
            confirmContentChange: false
        );

        // Assert
        changeResult.ShouldBe(ArchiveFolderChangeResult.Relocated);

        DbContext.ChangeTracker.Clear();
        var result = await DbContext
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
    public async Task SetArchiveFolderAsync_OldRarVolumesInOtherFolder_RelocatesArchive()
    {
        // Arrange
        var currentFolderPath = CreateReleaseFolderWithFiles();
        var archiveConfig = await AddUnmanagedArchiveConfigAsync(
            currentFolderPath,
            "bearcat-release.rar",
            "bearcat-release.r00",
            "bearcat-release.r01"
        );
        var targetFolderPath = CreateReleaseFolderWithFiles(
            "bearcat-release.rar",
            "bearcat-release.r00",
            "bearcat-release.r01",
            "bearcat-release.nfo"
        );

        // Act
        var changeResult = await service.SetArchiveFolderAsync(
            archiveConfig.Id,
            targetFolderPath,
            confirmContentChange: false
        );

        // Assert
        changeResult.ShouldBe(ArchiveFolderChangeResult.Relocated);

        DbContext.ChangeTracker.Clear();
        var result = await DbContext
            .ArchiveConfigs.AsSplitQuery()
            .Include(c => c.Archives)
                .ThenInclude(a => a.ArchiveFiles)
            .SingleAsync(c => c.Id == archiveConfig.Id);
        var archive = result.Archives.Single();

        result.ArchiveFilesBasePath.ShouldBe(targetFolderPath);
        archive.ArchiveFolderPath.ShouldBe(targetFolderPath);
        archive
            .ArchiveFiles.Select(file => file.FullFileName)
            .Order(StringComparer.Ordinal)
            .ShouldBe([
                Path.Combine(targetFolderPath, "bearcat-release.r00"),
                Path.Combine(targetFolderPath, "bearcat-release.r01"),
                Path.Combine(targetFolderPath, "bearcat-release.rar"),
            ]);
    }

    [Test]
    public async Task SetArchiveFolderAsync_ArchiveFilesChanged_RequiresConfirmationThenReimports()
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
        await File.WriteAllTextAsync(
            Path.Combine(releaseFolderPath, "Bearcat.Release.Unmanaged.rar"),
            "new"
        );

        // Act
        var unconfirmedResult = await service.SetArchiveFolderAsync(
            archiveConfig.Id,
            releaseFolderPath,
            confirmContentChange: false
        );
        var confirmedResult = await service.SetArchiveFolderAsync(
            archiveConfig.Id,
            releaseFolderPath,
            confirmContentChange: true
        );

        // Assert
        unconfirmedResult.ShouldBe(ArchiveFolderChangeResult.ConfirmationRequired);
        confirmedResult.ShouldBe(ArchiveFolderChangeResult.Reimported);

        DbContext.ChangeTracker.Clear();
        var result = await DbContext
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
    public async Task SetArchiveFolderAsync_TargetHasAdditionalArchiveFiles_RequiresConfirmationThenReimports()
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
        var unconfirmedResult = await service.SetArchiveFolderAsync(
            archiveConfig.Id,
            releaseFolderPath,
            confirmContentChange: false
        );
        var confirmedResult = await service.SetArchiveFolderAsync(
            archiveConfig.Id,
            releaseFolderPath,
            confirmContentChange: true
        );

        // Assert
        unconfirmedResult.ShouldBe(ArchiveFolderChangeResult.ConfirmationRequired);
        confirmedResult.ShouldBe(ArchiveFolderChangeResult.Reimported);

        DbContext.ChangeTracker.Clear();
        var result = await DbContext
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
    public async Task SetArchiveFolderAsync_NoMatchingArchiveFiles_ThrowsInvalidOperationException()
    {
        // Arrange
        var releaseFolderPath = CreateReleaseFolderWithFiles("Bearcat.Release.Unmanaged.part1.rar");
        var archiveConfig = await AddUnmanagedArchiveConfigAsync(
            releaseFolderPath,
            "Bearcat.Release.Unmanaged.part1.rar"
        );
        File.Delete(Path.Combine(releaseFolderPath, "Bearcat.Release.Unmanaged.part1.rar"));
        await File.WriteAllTextAsync(
            Path.Combine(releaseFolderPath, "Bearcat.Release.Unmanaged.zip"),
            "zip"
        );

        // Act
        var result = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.SetArchiveFolderAsync(
                archiveConfig.Id,
                releaseFolderPath,
                confirmContentChange: false
            )
        );

        // Assert
        result.Message.ShouldBe(
            $"Archive folder path {releaseFolderPath} does not contain archive files for archiver RAR."
        );
    }

    [Test]
    public async Task SetArchiveFolderAsync_ManagedReleaseArchiveConfig_ThrowsInvalidOperationException()
    {
        // Arrange
        var archiveConfig = await AddArchiveConfigAsync();

        // Act
        var result = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.SetArchiveFolderAsync(
                archiveConfig.Id,
                "/data/releases",
                confirmContentChange: false
            )
        );

        // Assert
        result.Message.ShouldBe("Archives can only be refreshed for unmanaged releases.");
    }

    private async Task<List<AdditionalArchiveContent>> AddAdditionalArchiveContentsAsync()
    {
        List<AdditionalArchiveContent> additionalArchiveContents =
        [
            new AdditionalArchiveContent
            {
                Name = "Premium ad folder",
                Type = AdditionalArchiveContentType.Path,
                SourcePath = "/data/ads/premium",
            },
            new AdditionalArchiveContent
            {
                Name = "Premium ad text",
                Type = AdditionalArchiveContentType.TextFile,
                FileName = "buy premium via me.txt",
                TextContent = "Buy premium via my link.",
            },
        ];

        DbContext.AdditionalArchiveContents.AddRange(additionalArchiveContents);
        await DbContext.SaveChangesAsync();

        return additionalArchiveContents;
    }

    private async Task<
        List<AdditionalArchiveContent>
    > AddAdditionalArchiveContentsWithSameEntryNameAsync()
    {
        List<AdditionalArchiveContent> additionalArchiveContents =
        [
            new AdditionalArchiveContent
            {
                Name = "Premium ad folder",
                Type = AdditionalArchiveContentType.Path,
                SourcePath = "/data/ads/premium/",
            },
            new AdditionalArchiveContent
            {
                Name = "Premium ad text",
                Type = AdditionalArchiveContentType.TextFile,
                FileName = "PREMIUM",
                TextContent = "Buy premium via my link.",
            },
        ];

        DbContext.AdditionalArchiveContents.AddRange(additionalArchiveContents);
        await DbContext.SaveChangesAsync();

        return additionalArchiveContents;
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

        DbContext.ArchiveConfigs.Add(archiveConfig);
        await DbContext.SaveChangesAsync();

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

        DbContext.ArchiveConfigs.Add(archiveConfig);
        await DbContext.SaveChangesAsync();

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

        DbContext.Releases.Add(release);
        await DbContext.SaveChangesAsync();

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
