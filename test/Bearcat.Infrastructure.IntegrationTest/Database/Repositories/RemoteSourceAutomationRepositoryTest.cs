using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class RemoteSourceAutomationRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime DiscoveredAt = new(
        2026,
        10,
        8,
        12,
        0,
        0,
        DateTimeKind.Unspecified
    );

    [Test]
    public async Task GetExistingReleaseOrDownloadFolderNamesAsync_DownloadWithSameFolderName_ReturnsName()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var automation = await AddAutomationAsync(dbContext);
        AddDownload(dbContext, automation, "Show.S01E01-GRP", RemoteSourceDownloadState.Failed);
        await dbContext.SaveChangesAsync();
        var repository = CreateRepository();

        // Act
        var existingFolderNames = await repository.GetExistingReleaseOrDownloadFolderNamesAsync([
            "Show.S01E01-GRP",
            "Show.S01E02-GRP",
        ]);

        // Assert
        existingFolderNames.ShouldBe(["show.s01e01-grp"], ignoreOrder: true);
    }

    [Test]
    public async Task GetExistingReleaseOrDownloadFolderNamesAsync_ReleaseWithSameName_ReturnsName()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var automation = await AddAutomationAsync(dbContext);
        AddRelease(dbContext, automation, "Movie.2026.1080p-GRP");
        await dbContext.SaveChangesAsync();
        var repository = CreateRepository();

        // Act
        var existingFolderNames = await repository.GetExistingReleaseOrDownloadFolderNamesAsync([
            "Movie.2026.1080p-GRP",
            "Movie.2026.720p-GRP",
        ]);

        // Assert
        existingFolderNames.ShouldBe(["movie.2026.1080p-grp"], ignoreOrder: true);
    }

    [Test]
    public async Task GetExistingReleaseOrDownloadFolderNamesAsync_NamesDifferOnlyInCase_ReturnsLowercaseNamesOnce()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var automation = await AddAutomationAsync(dbContext);
        AddDownload(dbContext, automation, "SHOW.S01E01-GRP", RemoteSourceDownloadState.Duplicate);
        AddRelease(dbContext, automation, "show.s01e01-grp");
        AddRelease(dbContext, automation, "Movie.2026.1080P-grp");
        await dbContext.SaveChangesAsync();
        var repository = CreateRepository();

        // Act
        var existingFolderNames = await repository.GetExistingReleaseOrDownloadFolderNamesAsync([
            "Show.S01E01-GRP",
            "movie.2026.1080p-GRP",
        ]);

        // Assert
        existingFolderNames.ShouldBe(
            ["show.s01e01-grp", "movie.2026.1080p-grp"],
            ignoreOrder: true
        );
    }

    [Test]
    public async Task GetExistingReleaseOrDownloadFolderNamesAsync_NoMatchingDownloadOrRelease_ReturnsEmptySet()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var automation = await AddAutomationAsync(dbContext);
        AddDownload(dbContext, automation, "Show.S01E01-GRP", RemoteSourceDownloadState.Observing);
        AddRelease(dbContext, automation, "Movie.2026.1080p-GRP");
        await dbContext.SaveChangesAsync();
        var repository = CreateRepository();

        // Act
        var existingFolderNames = await repository.GetExistingReleaseOrDownloadFolderNamesAsync([
            "Show.S01E02-GRP",
            "Movie.2026",
        ]);

        // Assert
        existingFolderNames.ShouldBeEmpty();
    }

    private RemoteSourceAutomationRepository CreateRepository()
    {
        var dbContext = CreateDbContext();

        return new RemoteSourceAutomationRepository(dbContext, dbContext);
    }

    private static async Task<RemoteSourceAutomation> AddAutomationAsync(BearcatDbContext dbContext)
    {
        var automation = new RemoteSourceAutomation
        {
            Name = "Automation",
            RemoteSourceRegistration = new RemoteSourceRegistration
            {
                Name = "Main FTP",
                SerializedConfig = "{}",
                SourceClassName = "FtpRemoteSource",
                IsActive = true,
            },
            RemotePath = "/incoming",
            TargetPath = "/downloads",
            ReleaseTemplate = new ReleaseTemplate
            {
                Name = "Template",
                ReleaseType = ReleaseType.Managed,
                ReleaseGroup = new ReleaseGroup
                {
                    Name = "Group",
                    EnableAutomaticReuploads = false,
                    NumberOfHoursUntilReupload = 24,
                },
            },
            IsEnabled = true,
        };

        dbContext.RemoteSourceAutomations.Add(automation);
        await dbContext.SaveChangesAsync();

        return automation;
    }

    private static void AddDownload(
        BearcatDbContext dbContext,
        RemoteSourceAutomation automation,
        string folderName,
        RemoteSourceDownloadState state
    )
    {
        dbContext.RemoteSourceDownloads.Add(
            new RemoteSourceDownload
            {
                RemoteSourceAutomationId = automation.Id,
                RemoteSourceRegistrationId = automation.RemoteSourceRegistrationId,
                SourceName = "Main FTP",
                RemoteFolderPath = $"/incoming/{folderName}",
                FolderName = folderName,
                LocalFolderPath = $"/downloads/{folderName}",
                ReleaseTemplateId = automation.ReleaseTemplateId,
                State = state,
                FileCount = 1,
                TotalBytes = 100,
                LastChangedAt = DiscoveredAt,
                DiscoveredAt = DiscoveredAt,
            }
        );
    }

    private static void AddRelease(
        BearcatDbContext dbContext,
        RemoteSourceAutomation automation,
        string name
    )
    {
        dbContext.Releases.Add(
            new Release
            {
                Name = name,
                CreatedAt = DiscoveredAt,
                ReleaseType = ReleaseType.Managed,
                ReleaseGroupId = automation.ReleaseTemplate.ReleaseGroupId,
                ArchiveConfigs = [],
                UploadConfigs = [],
            }
        );
    }
}
