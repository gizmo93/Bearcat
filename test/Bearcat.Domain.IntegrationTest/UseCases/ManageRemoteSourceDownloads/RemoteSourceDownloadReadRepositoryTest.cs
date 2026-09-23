using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageRemoteSourceDownloads.Dto;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageRemoteSourceDownloads;

public class RemoteSourceDownloadReadRepositoryTest : BearcatIntegrationTest
{
    private static readonly DateTime DiscoveredAt = new(
        2026,
        9,
        23,
        12,
        0,
        0,
        DateTimeKind.Unspecified
    );

    [Test]
    public async Task SearchAsync_StateFilterAndPaging_ReturnsNewestMatchingDownloadsFirst()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var automation = await AddAutomationAsync(dbContext);
        AddDownload(dbContext, automation, "Oldest", RemoteSourceDownloadState.Failed, 0);
        AddDownload(dbContext, automation, "Ignored", RemoteSourceDownloadState.Ignored, 1);
        AddDownload(dbContext, automation, "Middle", RemoteSourceDownloadState.Pending, 2);
        AddDownload(dbContext, automation, "Newest", RemoteSourceDownloadState.Canceled, 3);
        await dbContext.SaveChangesAsync();
        var repository = new RemoteSourceDownloadReadRepository(CreateDbContext());

        // Act
        var result = await repository.SearchAsync(
            new RemoteSourceDownloadSearchQuery(
                States:
                [
                    RemoteSourceDownloadState.Failed,
                    RemoteSourceDownloadState.Pending,
                    RemoteSourceDownloadState.Canceled,
                ],
                PageIndex: 0,
                PageSize: 5
            )
        );

        // Assert
        result.TotalCount.ShouldBe(3);
        result
            .Items.Select(download => download.FolderName)
            .ShouldBe(["Newest", "Middle", "Oldest"]);
        result.Items.ShouldAllBe(download => download.AutomationName == "Automation");
    }

    [Test]
    public async Task GetRunningAsync_MixedStates_ReturnsActiveDownloadsWithRunningOnesFirst()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var automation = await AddAutomationAsync(dbContext);
        AddDownload(dbContext, automation, "Queued", RemoteSourceDownloadState.Pending, 0);
        AddDownload(dbContext, automation, "Observed", RemoteSourceDownloadState.Observing, 1);
        AddDownload(dbContext, automation, "Finished", RemoteSourceDownloadState.Downloaded, 2);
        AddDownload(dbContext, automation, "Running", RemoteSourceDownloadState.Downloading, 3);
        AddDownload(dbContext, automation, "Broken", RemoteSourceDownloadState.Failed, 4);
        await dbContext.SaveChangesAsync();
        var repository = new RemoteSourceDownloadReadRepository(CreateDbContext());

        // Act
        var downloads = await repository.GetRunningAsync();

        // Assert
        downloads
            .Select(download => download.FolderName)
            .ShouldBe(["Running", "Finished", "Queued"]);
    }

    [Test]
    public async Task GetByReleaseIdAsync_DownloadCreatedRelease_ReturnsOrigin()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var automation = await AddAutomationAsync(dbContext);
        var release = new Release
        {
            Name = "Show.S01E01-GRP",
            CreatedAt = DiscoveredAt,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/downloads/Show.S01E01-GRP",
            ReleaseGroup = automation.ReleaseTemplate.ReleaseGroup,
            ArchiveConfigs = [],
            UploadConfigs = [],
        };
        var download = AddDownload(
            dbContext,
            automation,
            "Show.S01E01-GRP",
            RemoteSourceDownloadState.ReleaseCreated,
            0
        );
        download.Release = release;
        download.CompletedAt = DiscoveredAt.AddHours(1);
        AddDownload(dbContext, automation, "Other-GRP", RemoteSourceDownloadState.Pending, 1);
        await dbContext.SaveChangesAsync();
        var repository = new RemoteSourceDownloadReadRepository(CreateDbContext());

        // Act
        var origin = await repository.GetByReleaseIdAsync(release.Id);

        // Assert
        origin.ShouldNotBeNull();
        origin.Id.ShouldBe(download.Id);
        origin.SourceName.ShouldBe("Main FTP");
        origin.RemoteFolderPath.ShouldBe("/incoming/Show.S01E01-GRP");
        origin.CompletedAt.ShouldBe(DiscoveredAt.AddHours(1));
        origin.ReleaseName.ShouldBe(release.Name);
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
            PrimaryLanguageCode = "de",
            IsEnabled = true,
        };

        dbContext.RemoteSourceAutomations.Add(automation);
        await dbContext.SaveChangesAsync();

        return automation;
    }

    private static RemoteSourceDownload AddDownload(
        BearcatDbContext dbContext,
        RemoteSourceAutomation automation,
        string folderName,
        RemoteSourceDownloadState state,
        int discoveredMinutesOffset
    )
    {
        var download = new RemoteSourceDownload
        {
            RemoteSourceAutomationId = automation.Id,
            RemoteSourceRegistrationId = automation.RemoteSourceRegistrationId,
            SourceName = "Main FTP",
            RemoteFolderPath = $"/incoming/{folderName}",
            FolderName = folderName,
            LocalFolderPath = $"/downloads/{folderName}",
            ReleaseTemplateId = automation.ReleaseTemplateId,
            PrimaryLanguageCode = "de",
            State = state,
            FileCount = 1,
            TotalBytes = 100,
            LastChangedAt = DiscoveredAt,
            DiscoveredAt = DiscoveredAt.AddMinutes(discoveredMinutesOffset),
        };

        dbContext.RemoteSourceDownloads.Add(download);

        return download;
    }
}
