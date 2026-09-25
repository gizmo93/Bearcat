using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageAdditionalArchiveContents;

public class AdditionalArchiveContentPersistenceTest : BearcatIntegrationTest
{
    [Test]
    public async Task SaveChangesAsync_PathAndTextFileContents_LoadsContentsWithAllProperties()
    {
        // Arrange
        var dbContext = CreateDbContext();
        dbContext.AdditionalArchiveContents.AddRange(
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
            }
        );

        // Act
        await dbContext.SaveChangesAsync();

        // Assert
        var contents = await CreateDbContext()
            .AdditionalArchiveContents.OrderBy(content => content.Name)
            .ToListAsync();

        contents.Count.ShouldBe(2);
        var additionalArchivePath = contents[0];
        additionalArchivePath.Name.ShouldBe("Premium ad folder");
        additionalArchivePath.Type.ShouldBe(AdditionalArchiveContentType.Path);
        additionalArchivePath.SourcePath.ShouldBe("/data/ads/premium");
        additionalArchivePath.FileName.ShouldBeNull();
        additionalArchivePath.TextContent.ShouldBeNull();
        var additionalArchiveTextFile = contents[1];
        additionalArchiveTextFile.Name.ShouldBe("Premium ad text");
        additionalArchiveTextFile.Type.ShouldBe(AdditionalArchiveContentType.TextFile);
        additionalArchiveTextFile.SourcePath.ShouldBeNull();
        additionalArchiveTextFile.FileName.ShouldBe("buy premium via me.txt");
        additionalArchiveTextFile.TextContent.ShouldBe("Buy premium via my link.");
    }

    [Test]
    public async Task SaveChangesAsync_PathContentWithFileName_ThrowsDbUpdateException()
    {
        // Arrange
        var dbContext = CreateDbContext();
        dbContext.AdditionalArchiveContents.Add(
            new AdditionalArchiveContent
            {
                Name = "Premium ad folder",
                Type = AdditionalArchiveContentType.Path,
                SourcePath = "/data/ads/premium",
                FileName = "buy premium via me.txt",
            }
        );

        // Act
        var result = await Should.ThrowAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());

        // Assert
        result
            .InnerException.ShouldBeOfType<PostgresException>()
            .ConstraintName.ShouldBe("CK_AdditionalArchiveContent_FieldsMatchType");
    }

    [Test]
    public async Task SaveChangesAsync_TextFileContentWithoutTextContent_ThrowsDbUpdateException()
    {
        // Arrange
        var dbContext = CreateDbContext();
        dbContext.AdditionalArchiveContents.Add(
            new AdditionalArchiveContent
            {
                Name = "Premium ad text",
                Type = AdditionalArchiveContentType.TextFile,
                FileName = "buy premium via me.txt",
            }
        );

        // Act
        var result = await Should.ThrowAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());

        // Assert
        result
            .InnerException.ShouldBeOfType<PostgresException>()
            .ConstraintName.ShouldBe("CK_AdditionalArchiveContent_FieldsMatchType");
    }

    [Test]
    public async Task SaveChangesAsync_DuplicateName_ThrowsDbUpdateException()
    {
        // Arrange
        var dbContext = CreateDbContext();
        dbContext.AdditionalArchiveContents.Add(
            new AdditionalArchiveContent
            {
                Name = "Premium ad",
                Type = AdditionalArchiveContentType.Path,
                SourcePath = "/data/ads/premium",
            }
        );
        await dbContext.SaveChangesAsync();
        var secondDbContext = CreateDbContext();
        secondDbContext.AdditionalArchiveContents.Add(
            new AdditionalArchiveContent
            {
                Name = "Premium ad",
                Type = AdditionalArchiveContentType.TextFile,
                FileName = "buy premium via me.txt",
                TextContent = "Buy premium via my link.",
            }
        );

        // Act
        var result = await Should.ThrowAsync<DbUpdateException>(() =>
            secondDbContext.SaveChangesAsync()
        );

        // Assert
        result.ShouldNotBeNull();
    }

    [Test]
    public async Task SaveChangesAsync_DeleteContentAssignedToArchiveConfigTemplate_ThrowsDbUpdateException()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();
        var dbContext = CreateDbContext();
        dbContext.ReleaseTemplates.Add(
            new ReleaseTemplate
            {
                Name = "Managed template",
                ReleaseType = ReleaseType.Managed,
                ReleaseGroupId = releaseGroup.Id,
                ArchiveConfigTemplates =
                [
                    new ArchiveConfigTemplate
                    {
                        Name = "RAR Forum A",
                        ArchiveFilesBasePath = "/tmp/archives",
                        ArchiverName = "rar",
                        ArchiveFileSizeMb = 1024,
                        AdditionalArchiveContents =
                        [
                            new AdditionalArchiveContent
                            {
                                Name = "Premium ad folder",
                                Type = AdditionalArchiveContentType.Path,
                                SourcePath = "/data/ads/premium",
                            },
                        ],
                    },
                ],
            }
        );
        await dbContext.SaveChangesAsync();
        var deletionDbContext = CreateDbContext();
        deletionDbContext.AdditionalArchiveContents.Remove(
            await deletionDbContext.AdditionalArchiveContents.SingleAsync()
        );

        // Act
        var result = await Should.ThrowAsync<DbUpdateException>(() =>
            deletionDbContext.SaveChangesAsync()
        );

        // Assert
        result.ShouldNotBeNull();
        (await CreateDbContext().AdditionalArchiveContents.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task SaveChangesAsync_DeleteContentAssignedToArchiveConfig_ThrowsDbUpdateException()
    {
        // Arrange
        var release = await AddReleaseAsync();
        var dbContext = CreateDbContext();
        dbContext.ArchiveConfigs.Add(
            new ArchiveConfig
            {
                ReleaseId = release.Id,
                Name = "RAR Forum A",
                ArchiveFilesBasePath = "/tmp/archives",
                ArchiverName = "rar",
                ArchiveFileSizeMb = 1024,
                AdditionalArchiveContents =
                [
                    new AdditionalArchiveContent
                    {
                        Name = "Premium ad text",
                        Type = AdditionalArchiveContentType.TextFile,
                        FileName = "buy premium via me.txt",
                        TextContent = "Buy premium via my link.",
                    },
                ],
            }
        );
        await dbContext.SaveChangesAsync();
        var deletionDbContext = CreateDbContext();
        deletionDbContext.AdditionalArchiveContents.Remove(
            await deletionDbContext.AdditionalArchiveContents.SingleAsync()
        );

        // Act
        var result = await Should.ThrowAsync<DbUpdateException>(() =>
            deletionDbContext.SaveChangesAsync()
        );

        // Assert
        result.ShouldNotBeNull();
        (await CreateDbContext().AdditionalArchiveContents.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task SaveChangesAsync_DeleteReleaseWithAssignedContent_RemovesAssignmentAndKeepsContent()
    {
        // Arrange
        var release = await AddReleaseAsync();
        var dbContext = CreateDbContext();
        dbContext.ArchiveConfigs.Add(
            new ArchiveConfig
            {
                ReleaseId = release.Id,
                Name = "RAR Forum A",
                ArchiveFilesBasePath = "/tmp/archives",
                ArchiverName = "rar",
                ArchiveFileSizeMb = 1024,
                AdditionalArchiveContents =
                [
                    new AdditionalArchiveContent
                    {
                        Name = "Premium ad folder",
                        Type = AdditionalArchiveContentType.Path,
                        SourcePath = "/data/ads/premium",
                    },
                ],
            }
        );
        await dbContext.SaveChangesAsync();
        var deletionDbContext = CreateDbContext();
        deletionDbContext.Releases.Remove(await deletionDbContext.Releases.SingleAsync());

        // Act
        await deletionDbContext.SaveChangesAsync();

        // Assert
        var verificationDbContext = CreateDbContext();
        (await verificationDbContext.ArchiveConfigs.AnyAsync()).ShouldBeFalse();
        (await verificationDbContext.AdditionalArchiveContents.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task SaveChangesAsync_ArchiveWithReleaseFolderEntriesCopiedForPacking_LoadsEntries()
    {
        // Arrange
        var release = await AddReleaseAsync();
        var dbContext = CreateDbContext();
        dbContext.ArchiveConfigs.Add(
            new ArchiveConfig
            {
                ReleaseId = release.Id,
                Name = "RAR Forum A",
                ArchiveFilesBasePath = "/tmp/archives",
                ArchiverName = "rar",
                ArchiveFileSizeMb = 1024,
                Archives =
                [
                    new Archive
                    {
                        ArchiveFolderPath = "/tmp/archives/Bearcat.Release.001",
                        CreatedAt = DateTime.UtcNow,
                        ArchiveState = ArchiveState.Creating,
                        ArchiveFileSizeMb = 1024,
                        ReleaseFolderEntriesCopiedForPacking =
                        [
                            "buy premium via me.txt",
                            "Premium ad folder",
                        ],
                    },
                ],
            }
        );

        // Act
        await dbContext.SaveChangesAsync();

        // Assert
        var archive = await CreateDbContext().Archives.SingleAsync();

        archive.ReleaseFolderEntriesCopiedForPacking.ShouldBe([
            "buy premium via me.txt",
            "Premium ad folder",
        ]);
    }

    [Test]
    public async Task SaveChangesAsync_ArchiveWithoutReleaseFolderEntriesCopiedForPacking_LoadsEmptyEntries()
    {
        // Arrange
        var release = await AddReleaseAsync();
        var dbContext = CreateDbContext();
        dbContext.ArchiveConfigs.Add(
            new ArchiveConfig
            {
                ReleaseId = release.Id,
                Name = "RAR Forum A",
                ArchiveFilesBasePath = "/tmp/archives",
                ArchiverName = "rar",
                ArchiveFileSizeMb = 1024,
                Archives =
                [
                    new Archive
                    {
                        ArchiveFolderPath = "/tmp/archives/Bearcat.Release.001",
                        CreatedAt = DateTime.UtcNow,
                        ArchiveState = ArchiveState.Creating,
                        ArchiveFileSizeMb = 1024,
                    },
                ],
            }
        );

        // Act
        await dbContext.SaveChangesAsync();

        // Assert
        var archive = await CreateDbContext().Archives.SingleAsync();

        archive.ReleaseFolderEntriesCopiedForPacking.ShouldBeEmpty();
    }

    private async Task<ReleaseGroup> AddReleaseGroupAsync()
    {
        var dbContext = CreateDbContext();
        var releaseGroup = new ReleaseGroup
        {
            Name = "Managed releases",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };

        dbContext.ReleaseGroups.Add(releaseGroup);
        await dbContext.SaveChangesAsync();

        return releaseGroup;
    }

    private async Task<Release> AddReleaseAsync()
    {
        var releaseGroup = await AddReleaseGroupAsync();
        var dbContext = CreateDbContext();
        var release = new Release
        {
            Name = "Bearcat.Release.001",
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/releases/Bearcat.Release.001",
            ReleaseGroupId = releaseGroup.Id,
        };

        dbContext.Releases.Add(release);
        await dbContext.SaveChangesAsync();

        return release;
    }
}
