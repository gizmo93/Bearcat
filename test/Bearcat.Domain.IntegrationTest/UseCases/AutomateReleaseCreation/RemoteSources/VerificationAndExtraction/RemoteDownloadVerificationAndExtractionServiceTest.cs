using System.IO.Hashing;
using System.Text;
using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Entities;
using Bearcat.Domain.IntegrationTest.Shared;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.VerificationAndExtraction;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.VerificationAndExtraction.Extraction;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.VerificationAndExtraction.SfvVerification;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.AutomateReleaseCreation.RemoteSources.VerificationAndExtraction;

public class RemoteDownloadVerificationAndExtractionServiceTest : BearcatIntegrationTest
{
    private const string ReleaseName = "Show.S01E01.German.1080p.WEB.x264-GRP";

    private static readonly DateTime StartTime = new(
        2026,
        9,
        27,
        12,
        0,
        0,
        DateTimeKind.Unspecified
    );

    private string tempRootPath = null!;
    private string localFolderPath = null!;

    [SetUp]
    public void Setup()
    {
        tempRootPath = Path.Combine(Path.GetTempPath(), $"bearcat-tests-{Guid.NewGuid():N}");
        localFolderPath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, "downloads", ReleaseName))
            .FullName;
    }

    [TearDown]
    public void DeleteTempFolder()
    {
        if (Directory.Exists(tempRootPath))
        {
            Directory.Delete(tempRootPath, recursive: true);
        }
    }

    [Test]
    public async Task ProcessAsync_DownloadedWithMatchingSfv_IsHandedOverToReleaseCreation()
    {
        // Arrange
        await WriteFileAsync("release.rar", "volume");
        await WriteFileAsync("release.sfv", CreateSfvLine("release.rar", "volume"));
        var download = await AddDownloadAsync(RemoteSourceDownloadState.Downloaded);

        // Act
        await ProcessAsync();

        // Assert
        var reloaded = await ReloadDownloadAsync(download.Id);
        reloaded.State.ShouldBe(RemoteSourceDownloadState.ReadyForReleaseCreation);
        reloaded.ErrorMessage.ShouldBeNull();
        var readyDownloads = await new RemoteSourceDownloadRepository(
            CreateDbContext()
        ).GetReadyForReleaseCreationWithoutReleaseAsync();
        readyDownloads.Select(ready => ready.Id).ShouldBe([download.Id]);
    }

    [Test]
    public async Task ProcessAsync_SfvMismatch_PersistsFailureAndNotification()
    {
        // Arrange
        await WriteFileAsync("release.rar", "corrupted");
        await WriteFileAsync("release.sfv", CreateSfvLine("release.rar", "volume"));
        var download = await AddDownloadAsync(RemoteSourceDownloadState.Downloaded);

        // Act
        await ProcessAsync();

        // Assert
        var reloaded = await ReloadDownloadAsync(download.Id);
        reloaded.State.ShouldBe(RemoteSourceDownloadState.Failed);
        reloaded.ErrorMessage!.ShouldContain("release.rar");
        reloaded.ErrorMessage!.ShouldContain("files were kept");
        var notification = await CreateDbContext().Notifications.SingleAsync();
        notification.NotificationKind.ShouldBe(NotificationKind.RemoteDownloadFailed);
        notification.Message.ShouldContain(ReleaseName);
    }

    [TestCase(RemoteSourceDownloadState.Verifying)]
    [TestCase(RemoteSourceDownloadState.Extracting)]
    public async Task ProcessAsync_InterruptedByCrash_DeletesTemporaryFolderAndProcessesAgain(
        RemoteSourceDownloadState interruptedState
    )
    {
        // Arrange
        await WriteFileAsync(
            Path.Combine(
                DownloadFolderArchiveExtractionService.TemporaryExtractionFolderName,
                "movie.mkv"
            ),
            "partial"
        );
        await WriteFileAsync("movie.mkv", "movie");
        var download = await AddDownloadAsync(interruptedState, extractArchives: true);

        // Act
        await ProcessAsync();

        // Assert
        var reloaded = await ReloadDownloadAsync(download.Id);
        reloaded.State.ShouldBe(RemoteSourceDownloadState.ReadyForReleaseCreation);
        reloaded.ArchivesExtractedAt.ShouldBeNull();
        Directory
            .Exists(
                Path.Combine(
                    localFolderPath,
                    DownloadFolderArchiveExtractionService.TemporaryExtractionFolderName
                )
            )
            .ShouldBeFalse();
        File.Exists(Path.Combine(localFolderPath, "movie.mkv")).ShouldBeTrue();
    }

    [TestCase(RemoteSourceDownloadState.Downloading)]
    [TestCase(RemoteSourceDownloadState.ReadyForReleaseCreation)]
    [TestCase(RemoteSourceDownloadState.Failed)]
    public async Task ProcessAsync_DownloadInOtherState_IsLeftUntouched(
        RemoteSourceDownloadState state
    )
    {
        // Arrange
        await WriteFileAsync("release.sfv", "invalid line");
        var download = await AddDownloadAsync(state);

        // Act
        await ProcessAsync();

        // Assert
        (await ReloadDownloadAsync(download.Id)).State.ShouldBe(state);
        (await CreateDbContext().Notifications.AnyAsync()).ShouldBeFalse();
    }

    private async Task ProcessAsync()
    {
        var dbContext = CreateDbContext();
        var timeProvider = new ControllableTimeProvider(StartTime);
        var archiverFactory = new Mock<IArchiverFactory>(MockBehavior.Strict);
        archiverFactory.Setup(factory => factory.GetArchiveExtractors()).Returns([]);

        var service = new RemoteDownloadVerificationAndExtractionService(
            new RemoteSourceDownloadRepository(dbContext),
            new SfvChecksumVerifier(
                new TransferProgressTracker(),
                NullLogger<SfvChecksumVerifier>.Instance
            ),
            new DownloadFolderArchiveExtractionService(
                archiverFactory.Object,
                new FileSystemService(),
                new TransferProgressTracker(),
                NullLogger<DownloadFolderArchiveExtractionService>.Instance
            ),
            new NotificationService(
                new NotificationRepository(dbContext),
                timeProvider,
                CreateNotificationConfigurationProvider()
            ),
            timeProvider,
            NullLogger<RemoteDownloadVerificationAndExtractionService>.Instance
        );

        await service.ProcessAsync(CancellationToken.None);
    }

    private async Task<RemoteSourceDownload> AddDownloadAsync(
        RemoteSourceDownloadState state,
        bool extractArchives = false
    )
    {
        var download = new RemoteSourceDownload
        {
            SourceName = "Main FTP",
            RemoteFolderPath = $"/incoming/{ReleaseName}",
            FolderName = ReleaseName,
            LocalFolderPath = localFolderPath,
            ExtractArchivesBeforeReleaseCreation = extractArchives,
            State = state,
            FileCount = 1,
            TotalBytes = 1,
            LastChangedAt = StartTime,
            DiscoveredAt = StartTime,
            StartedAt = StartTime,
            CompletedAt = StartTime,
        };

        var dbContext = CreateDbContext();
        dbContext.RemoteSourceDownloads.Add(download);
        await dbContext.SaveChangesAsync();

        return download;
    }

    private async Task<RemoteSourceDownload> ReloadDownloadAsync(int id)
    {
        return await CreateDbContext().RemoteSourceDownloads.SingleAsync(d => d.Id == id);
    }

    private async Task WriteFileAsync(string relativePath, string content)
    {
        var filePath = Path.Combine(localFolderPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, content);
    }

    private static string CreateSfvLine(string relativeFilePath, string content)
    {
        return $"{relativeFilePath} {Crc32.HashToUInt32(Encoding.UTF8.GetBytes(content)):X8}";
    }
}
