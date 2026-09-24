using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Media;
using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Abstractions.RemoteSource;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.IntegrationTest.Shared;
using Bearcat.Domain.Shared.MediaMetadataResolution;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.Creation;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Downloading;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.ReleaseCreation;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ReleaseInfoResolution;
using Bearcat.Domain.UseCases.ManageRemoteSources.Sessions;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.AutomateReleaseCreation.RemoteSources.Downloading;

public class RemoteSourceDownloadServiceTest : BearcatIntegrationTest
{
    private const string SourceClassName = nameof(FakeRemoteSource);
    private const string IncomingPath = "/incoming";
    private const string ReleaseName = "Show.S01E01.German.1080p.WEB.x264-GRP";

    private static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(20);

    private static readonly DateTime StartTime = new(
        2026,
        9,
        23,
        12,
        0,
        0,
        DateTimeKind.Unspecified
    );

    private Dictionary<string, FakeRemoteServer> servers = null!;
    private FakeRemoteSource remoteSource = null!;
    private ControllableTimeProvider timeProvider = null!;
    private RemoteSourceConfiguration remoteSourceConfiguration = null!;
    private RemoteSourceSessionPool sessionPool = null!;
    private TransferCancellationRegistry cancellationRegistry = null!;
    private TransferProgressTracker progressTracker = null!;
    private string tempRootPath = null!;
    private string targetPath = null!;

    [SetUp]
    public void Setup()
    {
        servers = new Dictionary<string, FakeRemoteServer>(StringComparer.Ordinal);
        remoteSource = new FakeRemoteSource(servers);
        timeProvider = new ControllableTimeProvider(StartTime);
        remoteSourceConfiguration = new RemoteSourceConfiguration
        {
            MaxParallelFileDownloads = 4,
            MaxDownloadAttempts = 3,
            DownloadRetryDelaySeconds = 0,
        };
        sessionPool = new RemoteSourceSessionPool(NullLogger<RemoteSourceSessionPool>.Instance);
        cancellationRegistry = new TransferCancellationRegistry();
        progressTracker = new TransferProgressTracker();
        tempRootPath = Path.Combine(Path.GetTempPath(), $"bearcat-tests-{Guid.NewGuid():N}");
        targetPath = Path.Combine(tempRootPath, "downloads");
        Directory.CreateDirectory(targetPath);
    }

    [TearDown]
    public async Task DisposeResourcesAsync()
    {
        await sessionPool.DisposeAsync();

        if (Directory.Exists(tempRootPath))
        {
            Directory.Delete(tempRootPath, recursive: true);
        }
    }

    [Test]
    public async Task ProcessAsync_PendingDownload_DownloadsAllFilesAndCreatesRelease()
    {
        // Arrange
        var server = AddServer("main");
        server.SetFolderFiles(
            IncomingPath,
            ReleaseName,
            new FakeRemoteFile("release.rar", 300),
            new FakeRemoteFile("release.r00", 200),
            new FakeRemoteFile("Subs/english.srt", 50)
        );
        var template = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var registration = await AddRegistrationAsync("main");
        var automation = await AddAutomationAsync(registration, template);
        var download = await AddDownloadAsync(automation, ReleaseName);

        // Act
        await ProcessAsync();

        // Assert
        var reloaded = await ReloadDownloadAsync(download.Id);
        reloaded.State.ShouldBe(RemoteSourceDownloadState.ReleaseCreated);
        reloaded.StartedAt.ShouldBe(StartTime);
        reloaded.CompletedAt.ShouldBe(StartTime);
        reloaded.FileCount.ShouldBe(3);
        reloaded.TotalBytes.ShouldBe(550);
        reloaded.ErrorMessage.ShouldBeNull();

        var localFolderPath = Path.Combine(targetPath, ReleaseName);
        new FileInfo(Path.Combine(localFolderPath, "release.rar")).Length.ShouldBe(300);
        new FileInfo(Path.Combine(localFolderPath, "release.r00")).Length.ShouldBe(200);
        new FileInfo(Path.Combine(localFolderPath, "Subs", "english.srt")).Length.ShouldBe(50);
        Directory.GetFiles(localFolderPath, "*.part", SearchOption.AllDirectories).ShouldBeEmpty();

        var release = await CreateDbContext().Releases.SingleAsync();
        reloaded.ReleaseId.ShouldBe(release.Id);
        release.Name.ShouldBe(ReleaseName);
        release.ReleaseFolderPath.ShouldBe(localFolderPath);
        release.PrimaryLanguageCode.ShouldBe("de");

        var notification = await CreateDbContext().Notifications.SingleAsync();
        notification.NotificationKind.ShouldBe(NotificationKind.ReleaseCreatedFromRemoteDownload);
        notification.ReleaseId.ShouldBe(release.Id);
        notification.Message.ShouldContain($"{IncomingPath}/{ReleaseName}");
        notification.Message.ShouldContain("Main FTP");

        var transferKey = new TransferKey(TransferKind.RemoteDownload, download.Id);
        progressTracker.Get(transferKey).ShouldBeNull();
        cancellationRegistry.RequestCancellation(transferKey).ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_MoreFilesThanConnections_NeverExceedsMaxConnectionsOfRegistration()
    {
        // Arrange
        var server = AddServer("main");
        server.DownloadDelay = TimeSpan.FromMilliseconds(40);
        server.SetFolder(IncomingPath, "First-GRP", 10, 10, 10, 10, 10, 10);
        server.SetFolder(IncomingPath, "Second-GRP", 10, 10, 10, 10, 10, 10);
        var template = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var registration = await AddRegistrationAsync("main", maxConnections: 2);
        var automation = await AddAutomationAsync(registration, template);
        await AddDownloadAsync(automation, "First-GRP");
        await AddDownloadAsync(automation, "Second-GRP");

        // Act
        await ProcessAsync();

        // Assert
        server.MaxConcurrentSessions.ShouldBe(2);
        server.MaxConcurrentDownloads.ShouldBe(2);
        server.DownloadAttempts.Count.ShouldBe(12);
        (await GetDownloadsAsync()).ShouldAllBe(download =>
            download.State == RemoteSourceDownloadState.ReleaseCreated
        );
    }

    [Test]
    public async Task ProcessAsync_MoreConnectionsThanParallelFileLimit_NeverExceedsGlobalFileLimit()
    {
        // Arrange
        remoteSourceConfiguration.MaxParallelFileDownloads = 3;
        var server = AddServer("main");
        server.DownloadDelay = TimeSpan.FromMilliseconds(40);
        server.SetFolder(IncomingPath, "First-GRP", 10, 10, 10, 10, 10, 10);
        server.SetFolder(IncomingPath, "Second-GRP", 10, 10, 10, 10, 10, 10);
        var template = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var registration = await AddRegistrationAsync("main", maxConnections: 10);
        var automation = await AddAutomationAsync(registration, template);
        await AddDownloadAsync(automation, "First-GRP");
        await AddDownloadAsync(automation, "Second-GRP");

        // Act
        await ProcessAsync();

        // Assert
        server.MaxConcurrentDownloads.ShouldBe(3);
        server.DownloadAttempts.Count.ShouldBe(12);
        (await GetDownloadsAsync()).ShouldAllBe(download =>
            download.State == RemoteSourceDownloadState.ReleaseCreated
        );
    }

    [Test]
    public async Task ProcessAsync_TransientFileFailure_RetriesWithNewSessionAndSucceeds()
    {
        // Arrange
        var server = AddServer("main");
        server.SetFolder(IncomingPath, ReleaseName, 100, 200);
        server.FailDownload("file1.rar", failureCount: 2);
        var template = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var registration = await AddRegistrationAsync("main");
        var automation = await AddAutomationAsync(registration, template);
        var download = await AddDownloadAsync(automation, ReleaseName);

        // Act
        await ProcessAsync();

        // Assert
        (await ReloadDownloadAsync(download.Id)).State.ShouldBe(
            RemoteSourceDownloadState.ReleaseCreated
        );
        server.DownloadAttempts.Count(path => path == "file1.rar").ShouldBe(3);
        server.DisposeCount.ShouldBeGreaterThanOrEqualTo(2);
        new FileInfo(Path.Combine(targetPath, ReleaseName, "file1.rar")).Length.ShouldBe(200);
    }

    [Test]
    public async Task ProcessAsync_PermanentFileFailure_FailsDeletesFolderAndNotifies()
    {
        // Arrange
        var server = AddServer("main");
        server.SetFolder(IncomingPath, ReleaseName, 100, 200);
        server.FailDownload("file1.rar");
        var template = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var registration = await AddRegistrationAsync("main");
        var automation = await AddAutomationAsync(registration, template);
        var download = await AddDownloadAsync(automation, ReleaseName);

        // Act
        await ProcessAsync();

        // Assert
        var reloaded = await ReloadDownloadAsync(download.Id);
        reloaded.State.ShouldBe(RemoteSourceDownloadState.Failed);
        reloaded.ErrorMessage.ShouldNotBeNull();
        reloaded.ErrorMessage.ShouldContain("file1.rar");
        reloaded.ErrorMessage.ShouldContain("3 attempts");
        reloaded.ErrorMessage.ShouldContain("Transfer of file1.rar was interrupted");
        reloaded.ReleaseId.ShouldBeNull();
        server.DownloadAttempts.Count(path => path == "file1.rar").ShouldBe(3);
        Directory.Exists(Path.Combine(targetPath, ReleaseName)).ShouldBeFalse();
        (await CreateDbContext().Releases.AnyAsync()).ShouldBeFalse();

        var notification = await CreateDbContext().Notifications.SingleAsync();
        notification.NotificationKind.ShouldBe(NotificationKind.RemoteDownloadFailed);
        notification.NotificationSeverity.ShouldBe(NotificationSeverity.Error);
        notification.Message.ShouldContain(ReleaseName);
    }

    [TestCase("../escape.rar")]
    [TestCase("/etc/escape.rar")]
    [TestCase("Subs/../../escape.rar")]
    public async Task ProcessAsync_UnsafeRelativePath_FailsWithoutDownloadingAnything(
        string unsafeRelativePath
    )
    {
        // Arrange
        var server = AddServer("main");
        server.SetFolderFiles(
            IncomingPath,
            ReleaseName,
            new FakeRemoteFile("release.rar", 100),
            new FakeRemoteFile(unsafeRelativePath, 100)
        );
        var template = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var registration = await AddRegistrationAsync("main");
        var automation = await AddAutomationAsync(registration, template);
        var download = await AddDownloadAsync(automation, ReleaseName);

        // Act
        await ProcessAsync();

        // Assert
        var reloaded = await ReloadDownloadAsync(download.Id);
        reloaded.State.ShouldBe(RemoteSourceDownloadState.Failed);
        reloaded.ErrorMessage!.ShouldContain(unsafeRelativePath);
        server.DownloadAttempts.ShouldBeEmpty();
        File.Exists(Path.Combine(targetPath, "escape.rar")).ShouldBeFalse();
        File.Exists(Path.Combine(tempRootPath, "escape.rar")).ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_LocalFolderAlreadyContainsFiles_FailsAndKeepsExistingFiles()
    {
        // Arrange
        var server = AddServer("main");
        server.SetFolder(IncomingPath, ReleaseName, 100);
        var template = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var registration = await AddRegistrationAsync("main");
        var automation = await AddAutomationAsync(registration, template);
        var download = await AddDownloadAsync(automation, ReleaseName);
        var existingFilePath = Path.Combine(targetPath, ReleaseName, "user-data.mkv");
        Directory.CreateDirectory(Path.GetDirectoryName(existingFilePath)!);
        await File.WriteAllTextAsync(existingFilePath, "keep me");

        // Act
        await ProcessAsync();

        // Assert
        var reloaded = await ReloadDownloadAsync(download.Id);
        reloaded.State.ShouldBe(RemoteSourceDownloadState.Failed);
        reloaded.ErrorMessage!.ShouldContain("already exists and is not empty");
        reloaded.StartedAt.ShouldBeNull();
        (await File.ReadAllTextAsync(existingFilePath)).ShouldBe("keep me");
        server.OpenCount.ShouldBe(0);
        (await CreateDbContext().Notifications.SingleAsync()).NotificationKind.ShouldBe(
            NotificationKind.RemoteDownloadFailed
        );
    }

    [Test]
    public async Task ProcessAsync_UserCancelsRunningDownload_CancelsAndDeletesFolderWithoutFailureNotification()
    {
        // Arrange
        var server = AddServer("main");
        server.SetFolder(IncomingPath, ReleaseName, 100, 200);
        server.BlockDownload("file1.rar");
        var template = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var registration = await AddRegistrationAsync("main");
        var automation = await AddAutomationAsync(registration, template);
        var download = await AddDownloadAsync(automation, ReleaseName);

        // Act
        var processing = ProcessAsync();
        await server.BlockedDownloadStarted.Task.WaitAsync(CompletionTimeout);
        var stateWhileDownloading = (await ReloadDownloadAsync(download.Id)).State;
        await CreateStateService().CancelDownloadAsync(download.Id);
        await processing.WaitAsync(CompletionTimeout);

        // Assert
        stateWhileDownloading.ShouldBe(RemoteSourceDownloadState.Downloading);
        (await ReloadDownloadAsync(download.Id)).State.ShouldBe(RemoteSourceDownloadState.Canceled);
        Directory.Exists(Path.Combine(targetPath, ReleaseName)).ShouldBeFalse();
        (await CreateDbContext().Notifications.AnyAsync()).ShouldBeFalse();
        (await CreateDbContext().Releases.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_QueuedDownloadCanceledWhileAnotherRuns_IsSkipped()
    {
        // Arrange
        var server = AddServer("main");
        server.SetFolderFiles(IncomingPath, "First-GRP", new FakeRemoteFile("blocked.rar", 10));
        server.SetFolder(IncomingPath, "Second-GRP", 10);
        server.BlockDownload("blocked.rar");
        var template = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var registration = await AddRegistrationAsync("main");
        var automation = await AddAutomationAsync(registration, template);
        var first = await AddDownloadAsync(automation, "First-GRP");
        var second = await AddDownloadAsync(automation, "Second-GRP");

        // Act
        var processing = ProcessAsync();
        await server.BlockedDownloadStarted.Task.WaitAsync(CompletionTimeout);
        await CreateStateService().CancelDownloadAsync(second.Id);
        await CreateStateService().CancelDownloadAsync(first.Id);
        await processing.WaitAsync(CompletionTimeout);

        // Assert
        (await ReloadDownloadAsync(first.Id)).State.ShouldBe(RemoteSourceDownloadState.Canceled);
        (await ReloadDownloadAsync(second.Id)).State.ShouldBe(RemoteSourceDownloadState.Canceled);
        server.RecursivelyListedPaths.ShouldBe([$"{IncomingPath}/First-GRP"]);
    }

    [Test]
    public async Task ProcessAsync_TwoPendingDownloads_ProcessesThemOneAfterAnother()
    {
        // Arrange
        var server = AddServer("main");
        server.DownloadDelay = TimeSpan.FromMilliseconds(40);
        server.SetFolder(IncomingPath, "First-GRP", 10);
        server.SetFolder(IncomingPath, "Second-GRP", 10);
        var template = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var registration = await AddRegistrationAsync("main", maxConnections: 4);
        var automation = await AddAutomationAsync(registration, template);
        await AddDownloadAsync(automation, "First-GRP");
        await AddDownloadAsync(automation, "Second-GRP");

        // Act
        await ProcessAsync();

        // Assert
        server.MaxConcurrentDownloads.ShouldBe(1);
        server.RecursivelyListedPaths.ShouldBe([
            $"{IncomingPath}/First-GRP",
            $"{IncomingPath}/Second-GRP",
        ]);
        (await GetDownloadsAsync()).ShouldAllBe(download =>
            download.State == RemoteSourceDownloadState.ReleaseCreated
        );
    }

    [Test]
    public async Task ProcessAsync_EmptyRemoteFolder_FailsWithoutDownloadingAnything()
    {
        // Arrange
        var server = AddServer("main");
        server.SetFolderFiles(IncomingPath, ReleaseName);
        var template = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var registration = await AddRegistrationAsync("main");
        var automation = await AddAutomationAsync(registration, template);
        var download = await AddDownloadAsync(automation, ReleaseName);

        // Act
        await ProcessAsync();

        // Assert
        var reloaded = await ReloadDownloadAsync(download.Id);
        reloaded.State.ShouldBe(RemoteSourceDownloadState.Failed);
        reloaded.ErrorMessage!.ShouldContain("contains no files");
        server.DownloadAttempts.ShouldBeEmpty();
        (await CreateDbContext().Notifications.SingleAsync()).NotificationKind.ShouldBe(
            NotificationKind.RemoteDownloadFailed
        );
    }

    [Test]
    public async Task ProcessAsync_InactiveRegistration_LeavesDownloadPending()
    {
        // Arrange
        var server = AddServer("main");
        server.SetFolder(IncomingPath, ReleaseName, 100);
        var template = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var registration = await AddRegistrationAsync("main", isActive: false);
        var automation = await AddAutomationAsync(registration, template);
        var download = await AddDownloadAsync(automation, ReleaseName);

        // Act
        await ProcessAsync();

        // Assert
        var reloaded = await ReloadDownloadAsync(download.Id);
        reloaded.State.ShouldBe(RemoteSourceDownloadState.Pending);
        reloaded.StartedAt.ShouldBeNull();
        server.OpenCount.ShouldBe(0);
        Directory.Exists(Path.Combine(targetPath, ReleaseName)).ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_InterruptedDownloadFromCrash_DiscardsPartialFilesAndDownloadsAgain()
    {
        // Arrange
        var server = AddServer("main");
        server.SetFolder(IncomingPath, ReleaseName, 100);
        var template = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var registration = await AddRegistrationAsync("main");
        var automation = await AddAutomationAsync(registration, template);
        var download = await AddDownloadAsync(
            automation,
            ReleaseName,
            RemoteSourceDownloadState.Downloading,
            startedAt: StartTime.AddHours(-1)
        );
        var staleFilePath = Path.Combine(targetPath, ReleaseName, "file0.rar.part");
        Directory.CreateDirectory(Path.GetDirectoryName(staleFilePath)!);
        await File.WriteAllTextAsync(staleFilePath, "partial");

        // Act
        await ProcessAsync();

        // Assert
        var reloaded = await ReloadDownloadAsync(download.Id);
        reloaded.State.ShouldBe(RemoteSourceDownloadState.ReleaseCreated);
        reloaded.StartedAt.ShouldBe(StartTime);
        File.Exists(staleFilePath).ShouldBeFalse();
        new FileInfo(Path.Combine(targetPath, ReleaseName, "file0.rar")).Length.ShouldBe(100);
    }

    [Test]
    public async Task ProcessAsync_InterruptedDownloadWhoseFolderNameDiffersFromLastSegment_IsQueuedAgainWithoutDeletingFolder()
    {
        // Arrange
        AddServer("main");
        var template = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var registration = await AddRegistrationAsync("main", isActive: false);
        var automation = await AddAutomationAsync(registration, template);
        var foreignFolderPath = Path.Combine(targetPath, "Foreign.Folder-GRP");
        var foreignFilePath = Path.Combine(foreignFolderPath, "important.mkv");
        Directory.CreateDirectory(foreignFolderPath);
        await File.WriteAllTextAsync(foreignFilePath, "keep me");
        var download = await AddDownloadAsync(
            automation,
            ReleaseName,
            RemoteSourceDownloadState.Downloading,
            localFolderPath: foreignFolderPath
        );

        // Act
        await ProcessAsync();

        // Assert
        (await ReloadDownloadAsync(download.Id)).State.ShouldBe(RemoteSourceDownloadState.Pending);
        File.Exists(foreignFilePath).ShouldBeTrue();
    }

    [Test]
    public async Task ProcessAsync_UnmanagedTemplateWithoutArchives_FailsAndKeepsDownloadedFiles()
    {
        // Arrange
        var server = AddServer("main");
        server.SetFolderFiles(IncomingPath, ReleaseName, new FakeRemoteFile("video.mkv", 100));
        var template = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        var registration = await AddRegistrationAsync("main");
        var automation = await AddAutomationAsync(registration, template);
        var download = await AddDownloadAsync(automation, ReleaseName);

        // Act
        await ProcessAsync();

        // Assert
        var reloaded = await ReloadDownloadAsync(download.Id);
        reloaded.State.ShouldBe(RemoteSourceDownloadState.Failed);
        reloaded.ErrorMessage!.ShouldContain("No release could be created from template");
        reloaded.ErrorMessage!.ShouldContain("files were kept");
        reloaded.ReleaseId.ShouldBeNull();
        File.Exists(Path.Combine(targetPath, ReleaseName, "video.mkv")).ShouldBeTrue();
        (await CreateDbContext().Releases.AnyAsync()).ShouldBeFalse();
        (await CreateDbContext().Notifications.SingleAsync()).NotificationKind.ShouldBe(
            NotificationKind.RemoteDownloadFailed
        );
    }

    [Test]
    public async Task ProcessAsync_DownloadedButTemplateWasDeleted_FailsAndKeepsFiles()
    {
        // Arrange
        AddServer("main");
        var template = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var registration = await AddRegistrationAsync("main");
        var automation = await AddAutomationAsync(registration, template);
        var download = await AddDownloadAsync(
            automation,
            ReleaseName,
            RemoteSourceDownloadState.Downloaded,
            startedAt: StartTime,
            hasReleaseTemplate: false
        );
        var downloadedFilePath = Path.Combine(targetPath, ReleaseName, "file0.rar");
        Directory.CreateDirectory(Path.GetDirectoryName(downloadedFilePath)!);
        await File.WriteAllTextAsync(downloadedFilePath, "content");

        // Act
        await ProcessAsync();

        // Assert
        var reloaded = await ReloadDownloadAsync(download.Id);
        reloaded.State.ShouldBe(RemoteSourceDownloadState.Failed);
        reloaded.ErrorMessage!.ShouldContain("release template");
        reloaded.ErrorMessage!.ShouldContain("deleted");
        File.Exists(downloadedFilePath).ShouldBeTrue();
    }

    [Test]
    public async Task RestartDownloadAsync_FailedDownload_DeletesFolderAndQueuesAgain()
    {
        // Arrange
        AddServer("main");
        var template = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var registration = await AddRegistrationAsync("main");
        var automation = await AddAutomationAsync(registration, template);
        var download = await AddDownloadAsync(
            automation,
            ReleaseName,
            RemoteSourceDownloadState.Failed,
            startedAt: StartTime,
            errorMessage: "Previous error"
        );
        var keptFilePath = Path.Combine(targetPath, ReleaseName, "video.mkv");
        Directory.CreateDirectory(Path.GetDirectoryName(keptFilePath)!);
        await File.WriteAllTextAsync(keptFilePath, "content");

        // Act
        await CreateStateService().RestartDownloadAsync(download.Id);

        // Assert
        var reloaded = await ReloadDownloadAsync(download.Id);
        reloaded.State.ShouldBe(RemoteSourceDownloadState.Pending);
        reloaded.ErrorMessage.ShouldBeNull();
        reloaded.StartedAt.ShouldBeNull();
        reloaded.CompletedAt.ShouldBeNull();
        Directory.Exists(Path.Combine(targetPath, ReleaseName)).ShouldBeFalse();
    }

    [Test]
    public async Task RestartDownloadAsync_FailedBeforeStartBecauseFolderExisted_KeepsExistingFolder()
    {
        // Arrange
        AddServer("main");
        var template = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var registration = await AddRegistrationAsync("main");
        var automation = await AddAutomationAsync(registration, template);
        var download = await AddDownloadAsync(
            automation,
            ReleaseName,
            RemoteSourceDownloadState.Failed,
            errorMessage: "The local folder already exists and is not empty"
        );
        var userFilePath = Path.Combine(targetPath, ReleaseName, "user-data.mkv");
        Directory.CreateDirectory(Path.GetDirectoryName(userFilePath)!);
        await File.WriteAllTextAsync(userFilePath, "keep me");

        // Act
        await CreateStateService().RestartDownloadAsync(download.Id);

        // Assert
        (await ReloadDownloadAsync(download.Id)).State.ShouldBe(RemoteSourceDownloadState.Pending);
        File.Exists(userFilePath).ShouldBeTrue();
    }

    [TestCase(RemoteSourceDownloadState.Observing)]
    [TestCase(RemoteSourceDownloadState.Pending)]
    public async Task CancelDownloadAsync_NotYetStartedDownload_IsCanceled(
        RemoteSourceDownloadState state
    )
    {
        // Arrange
        AddServer("main");
        var template = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var registration = await AddRegistrationAsync("main");
        var automation = await AddAutomationAsync(registration, template);
        var download = await AddDownloadAsync(automation, ReleaseName, state);

        // Act
        await CreateStateService().CancelDownloadAsync(download.Id);

        // Assert
        (await ReloadDownloadAsync(download.Id)).State.ShouldBe(RemoteSourceDownloadState.Canceled);
    }

    [TestCase(RemoteSourceDownloadState.Observing)]
    [TestCase(RemoteSourceDownloadState.Pending)]
    [TestCase(RemoteSourceDownloadState.Failed)]
    [TestCase(RemoteSourceDownloadState.Canceled)]
    public async Task IgnoreDownloadAsync_IgnorableState_IsIgnored(RemoteSourceDownloadState state)
    {
        // Arrange
        AddServer("main");
        var template = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var registration = await AddRegistrationAsync("main");
        var automation = await AddAutomationAsync(registration, template);
        var download = await AddDownloadAsync(automation, ReleaseName, state);

        // Act
        await CreateStateService().IgnoreDownloadAsync(download.Id);

        // Assert
        (await ReloadDownloadAsync(download.Id)).State.ShouldBe(RemoteSourceDownloadState.Ignored);
    }

    [Test]
    public async Task DownloadAsync_PendingDownload_LeavesReleaseCreationToReleaseCreator()
    {
        // Arrange
        var server = AddServer("main");
        server.SetFolder(IncomingPath, ReleaseName, 100);
        var template = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var registration = await AddRegistrationAsync("main");
        var automation = await AddAutomationAsync(registration, template);
        var download = await AddDownloadAsync(automation, ReleaseName);

        // Act
        await DownloadAsync();

        // Assert
        var reloaded = await ReloadDownloadAsync(download.Id);
        reloaded.State.ShouldBe(RemoteSourceDownloadState.Downloaded);
        reloaded.CompletedAt.ShouldBe(StartTime);
        reloaded.ReleaseId.ShouldBeNull();
        (await CreateDbContext().Releases.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task RetryReleaseCreationAsync_FailedAfterCompleteDownload_CreatesReleaseFromKeptFiles()
    {
        // Arrange
        AddServer("main");
        var template = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var registration = await AddRegistrationAsync("main");
        var automation = await AddAutomationAsync(registration, template);
        var download = await AddDownloadAsync(
            automation,
            ReleaseName,
            RemoteSourceDownloadState.Failed,
            startedAt: StartTime,
            completedAt: StartTime,
            errorMessage: "No release could be created"
        );
        var keptFilePath = Path.Combine(targetPath, ReleaseName, "release.rar");
        Directory.CreateDirectory(Path.GetDirectoryName(keptFilePath)!);
        await File.WriteAllTextAsync(keptFilePath, "content");

        // Act
        await CreateStateService().RetryReleaseCreationAsync(download.Id);
        var retried = await ReloadDownloadAsync(download.Id);
        await CreateReleasesAsync();

        // Assert
        retried.State.ShouldBe(RemoteSourceDownloadState.Downloaded);
        retried.ErrorMessage.ShouldBeNull();
        retried.CompletedAt.ShouldBe(StartTime);

        var reloaded = await ReloadDownloadAsync(download.Id);
        reloaded.State.ShouldBe(RemoteSourceDownloadState.ReleaseCreated);
        var release = await CreateDbContext().Releases.SingleAsync();
        reloaded.ReleaseId.ShouldBe(release.Id);
        File.Exists(keptFilePath).ShouldBeTrue();
    }

    [Test]
    public async Task RetryReleaseCreationAsync_FailedBeforeDownloadCompleted_IsRejected()
    {
        // Arrange
        AddServer("main");
        var template = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var registration = await AddRegistrationAsync("main");
        var automation = await AddAutomationAsync(registration, template);
        var download = await AddDownloadAsync(
            automation,
            ReleaseName,
            RemoteSourceDownloadState.Failed,
            startedAt: StartTime,
            errorMessage: "Download failed"
        );

        // Act
        var retry = () => CreateStateService().RetryReleaseCreationAsync(download.Id);

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(retry);
        var reloaded = await ReloadDownloadAsync(download.Id);
        reloaded.State.ShouldBe(RemoteSourceDownloadState.Failed);
        reloaded.ErrorMessage.ShouldBe("Download failed");
    }

    [TestCase(RemoteSourceDownloadState.Observing)]
    [TestCase(RemoteSourceDownloadState.Pending)]
    [TestCase(RemoteSourceDownloadState.Canceled)]
    public async Task RetryReleaseCreationAsync_NotFailedDownload_IsRejected(
        RemoteSourceDownloadState state
    )
    {
        // Arrange
        AddServer("main");
        var template = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var registration = await AddRegistrationAsync("main");
        var automation = await AddAutomationAsync(registration, template);
        var download = await AddDownloadAsync(
            automation,
            ReleaseName,
            state,
            startedAt: StartTime,
            completedAt: StartTime
        );

        // Act
        var retry = () => CreateStateService().RetryReleaseCreationAsync(download.Id);

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(retry);
        (await ReloadDownloadAsync(download.Id)).State.ShouldBe(state);
    }

    [TestCase(RemoteSourceDownloadState.Downloaded)]
    [TestCase(RemoteSourceDownloadState.ReleaseCreated)]
    public async Task StateChanges_CompletedDownload_AreRejected(RemoteSourceDownloadState state)
    {
        // Arrange
        AddServer("main");
        var template = await AddReleaseTemplateAsync(ReleaseType.Managed);
        var registration = await AddRegistrationAsync("main");
        var automation = await AddAutomationAsync(registration, template);
        var download = await AddDownloadAsync(automation, ReleaseName, state);
        var stateService = CreateStateService();

        // Act
        var cancel = () => stateService.CancelDownloadAsync(download.Id);
        var restart = () => stateService.RestartDownloadAsync(download.Id);
        var ignore = () => stateService.IgnoreDownloadAsync(download.Id);
        var retryReleaseCreation = () => stateService.RetryReleaseCreationAsync(download.Id);

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(cancel);
        await Should.ThrowAsync<InvalidOperationException>(restart);
        await Should.ThrowAsync<InvalidOperationException>(ignore);
        await Should.ThrowAsync<InvalidOperationException>(retryReleaseCreation);
        (await ReloadDownloadAsync(download.Id)).State.ShouldBe(state);
    }

    private async Task ProcessAsync()
    {
        await DownloadAsync();
        await CreateReleasesAsync();
    }

    private async Task DownloadAsync()
    {
        var dbContext = CreateDbContext();

        var service = new RemoteSourceDownloadService(
            new RemoteSourceDownloadRepository(dbContext),
            CreateSessionProvider(),
            CreateFolderService(),
            progressTracker,
            cancellationRegistry,
            CreateNotificationService(dbContext),
            CreateConfigurationProvider(),
            timeProvider,
            NullLogger<RemoteSourceDownloadService>.Instance
        );

        await service.ProcessAsync(CancellationToken.None);
    }

    private async Task CreateReleasesAsync()
    {
        var dbContext = CreateDbContext();

        var releaseCreator = new RemoteDownloadReleaseCreator(
            new RemoteSourceDownloadRepository(dbContext),
            CreateReleaseFromFolderCreator(dbContext),
            CreateNotificationService(dbContext),
            timeProvider,
            NullLogger<RemoteDownloadReleaseCreator>.Instance
        );

        await releaseCreator.ProcessAsync(CancellationToken.None);
    }

    private NotificationService CreateNotificationService(BearcatDbContext dbContext)
    {
        return new NotificationService(
            new NotificationRepository(dbContext),
            timeProvider,
            CreateNotificationConfigurationProvider()
        );
    }

    private RemoteSourceDownloadStateService CreateStateService()
    {
        return new RemoteSourceDownloadStateService(
            new RemoteSourceDownloadRepository(CreateDbContext()),
            cancellationRegistry,
            CreateFolderService()
        );
    }

    private static RemoteDownloadFolderService CreateFolderService()
    {
        return new RemoteDownloadFolderService(
            new FileSystemService(),
            NullLogger<RemoteDownloadFolderService>.Instance
        );
    }

    private RemoteSourceSessionProvider CreateSessionProvider()
    {
        var factory = new Mock<IRemoteSourceFactory>(MockBehavior.Strict);
        factory.Setup(f => f.GetByClassName(SourceClassName)).Returns(remoteSource);
        var secretProtector = new Mock<ISecretProtector>(MockBehavior.Strict);
        secretProtector
            .Setup(p => p.Unprotect(It.IsAny<string>()))
            .Returns((string value) => value);

        return new RemoteSourceSessionProvider(sessionPool, factory.Object, secretProtector.Object);
    }

    private IApplicationConfigurationProvider CreateConfigurationProvider()
    {
        var configurationProvider = new Mock<IApplicationConfigurationProvider>(
            MockBehavior.Strict
        );
        configurationProvider
            .Setup(provider => provider.GetConfiguration<RemoteSourceConfiguration>())
            .Returns(() => remoteSourceConfiguration);

        return configurationProvider.Object;
    }

    private ReleaseFromFolderCreator CreateReleaseFromFolderCreator(BearcatDbContext dbContext)
    {
        var nfoDatabaseFactory = new Mock<INfoDatabaseFactory>(MockBehavior.Strict);
        nfoDatabaseFactory
            .Setup(factory => factory.GetByClassName())
            .Returns(new Dictionary<string, INfoDatabase>());
        var releaseInfoRepository = new ReleaseInfoRepository(
            dbContext,
            dbContext,
            NoOpSecretProtector.Instance
        );
        var classificationService = new ReleaseClassificationService(
            new ReleaseClassificationRepository(dbContext),
            timeProvider,
            NullLogger<ReleaseClassificationService>.Instance
        );
        var mediaMetadataExtractor = new Mock<IMediaMetadataExtractor>();
        mediaMetadataExtractor
            .Setup(extractor =>
                extractor.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((MediaProbeResult?)null);
        var archiverFactory = new Mock<IArchiverFactory>();
        archiverFactory
            .Setup(factory => factory.GetArchivers())
            .Returns([new ArchiverDto("RAR", "RarArchiver", ".rar")]);

        return new ReleaseFromFolderCreator(
            releaseInfoResolutionService: new ReleaseInfoResolutionService(
                releaseInfoRepository,
                nfoDatabaseFactory.Object,
                new ReleaseNfoResolver(
                    nfoDatabaseFactory.Object,
                    NullLogger<ReleaseNfoResolver>.Instance
                ),
                new ReleaseInfoResolver(
                    releaseInfoRepository,
                    nfoDatabaseFactory.Object,
                    NullLogger<ReleaseInfoResolver>.Instance
                ),
                new ReleaseMetadataResolver(
                    new MediaMetadataResolver(
                        new MediaMetadataResolverRepository(
                            dbContext,
                            NoOpSecretProtector.Instance
                        ),
                        new Mock<IMediaMetadataDatabaseFactory>(MockBehavior.Strict).Object,
                        NullLogger<MediaMetadataResolver>.Instance
                    ),
                    nfoDatabaseFactory.Object,
                    NullLogger<ReleaseMetadataResolver>.Instance
                ),
                classificationService,
                NullLogger<ReleaseInfoResolutionService>.Instance,
                timeProvider
            ),
            mediaMetadataService: new MediaMetadataService(
                new MediaMetadataRepository(dbContext),
                mediaMetadataExtractor.Object,
                new FileSystemService(),
                classificationService,
                timeProvider,
                NullLogger<MediaMetadataService>.Instance
            ),
            archiverFactory: archiverFactory.Object,
            releaseCollectionAssigner: new ReleaseCollectionAssignmentService(
                new ReleaseCollectionRepository(
                    dbRead: dbContext,
                    dbWrite: dbContext,
                    metadataDatabaseFactory: Mock.Of<IMediaMetadataDatabaseFactory>()
                ),
                timeProvider
            )
        );
    }

    private FakeRemoteServer AddServer(string key)
    {
        var server = new FakeRemoteServer();
        servers[key] = server;

        return server;
    }

    private async Task<List<RemoteSourceDownload>> GetDownloadsAsync()
    {
        return await CreateDbContext().RemoteSourceDownloads.OrderBy(d => d.Id).ToListAsync();
    }

    private async Task<RemoteSourceDownload> ReloadDownloadAsync(int id)
    {
        return await CreateDbContext().RemoteSourceDownloads.SingleAsync(d => d.Id == id);
    }

    private async Task<ReleaseTemplate> AddReleaseTemplateAsync(ReleaseType releaseType)
    {
        var template = new ReleaseTemplate
        {
            Name = $"{releaseType} template",
            ReleaseType = releaseType,
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{releaseType} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };

        var dbContext = CreateDbContext();
        dbContext.ReleaseTemplates.Add(template);
        await dbContext.SaveChangesAsync();

        return template;
    }

    private async Task<RemoteSourceRegistration> AddRegistrationAsync(
        string serverKey,
        int maxConnections = RemoteSourceRegistration.DefaultMaxConnections,
        bool isActive = true
    )
    {
        var registration = new RemoteSourceRegistration
        {
            Name = "Main FTP",
            SerializedConfig = serverKey,
            SourceClassName = SourceClassName,
            IsActive = isActive,
            MaxConnections = maxConnections,
        };

        var dbContext = CreateDbContext();
        dbContext.RemoteSourceRegistrations.Add(registration);
        await dbContext.SaveChangesAsync();

        return registration;
    }

    private async Task<RemoteSourceAutomation> AddAutomationAsync(
        RemoteSourceRegistration registration,
        ReleaseTemplate template
    )
    {
        var automation = new RemoteSourceAutomation
        {
            Name = "Automation",
            RemoteSourceRegistrationId = registration.Id,
            RemotePath = IncomingPath,
            TargetPath = targetPath,
            ReleaseTemplateId = template.Id,
            PrimaryLanguageCode = "de",
            IsEnabled = true,
        };

        var dbContext = CreateDbContext();
        dbContext.RemoteSourceAutomations.Add(automation);
        await dbContext.SaveChangesAsync();

        return automation;
    }

    private async Task<RemoteSourceDownload> AddDownloadAsync(
        RemoteSourceAutomation automation,
        string folderName,
        RemoteSourceDownloadState state = RemoteSourceDownloadState.Pending,
        DateTime? startedAt = null,
        DateTime? completedAt = null,
        string? errorMessage = null,
        string? localFolderPath = null,
        bool hasReleaseTemplate = true
    )
    {
        var download = new RemoteSourceDownload
        {
            RemoteSourceAutomationId = automation.Id,
            RemoteSourceRegistrationId = automation.RemoteSourceRegistrationId,
            SourceName = "Main FTP",
            RemoteFolderPath = FakeRemoteServer.CombinePath(automation.RemotePath, folderName),
            FolderName = folderName,
            LocalFolderPath = localFolderPath ?? Path.Combine(targetPath, folderName),
            ReleaseTemplateId = hasReleaseTemplate ? automation.ReleaseTemplateId : null,
            PrimaryLanguageCode = automation.PrimaryLanguageCode,
            KeepRawFiles = true,
            State = state,
            FileCount = 1,
            TotalBytes = 1,
            LastChangedAt = StartTime,
            DiscoveredAt = StartTime,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            ErrorMessage = errorMessage,
        };

        var dbContext = CreateDbContext();
        dbContext.RemoteSourceDownloads.Add(download);
        await dbContext.SaveChangesAsync();

        return download;
    }
}
