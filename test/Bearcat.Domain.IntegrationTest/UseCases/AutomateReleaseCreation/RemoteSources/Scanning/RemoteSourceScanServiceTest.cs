using System.Linq.Expressions;
using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.RemoteSource;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.IntegrationTest.Shared;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Scanning;
using Bearcat.Domain.UseCases.ManageRemoteSources.Sessions;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.AutomateReleaseCreation.RemoteSources.Scanning;

public class RemoteSourceScanServiceTest : BearcatIntegrationTest
{
    private const string SourceClassName = nameof(FakeRemoteSource);
    private const string IncomingPath = "/incoming";
    private const string TargetPath = "/data/downloads";

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

    [SetUp]
    public void Setup()
    {
        servers = new Dictionary<string, FakeRemoteServer>(StringComparer.Ordinal);
        remoteSource = new FakeRemoteSource(servers);
        timeProvider = new ControllableTimeProvider(StartTime);
        remoteSourceConfiguration = new RemoteSourceConfiguration
        {
            StabilityMinutes = 5,
            MinimumFolderSizeMegabytes = 0,
        };
    }

    [Test]
    public async Task ProcessAsync_NewFolder_CreatesObservingRecordWithSnapshots()
    {
        // Arrange
        var template = await AddReleaseTemplateAsync("German", ReleaseType.Managed);
        var server = AddServer("main");
        server.SetFolder(IncomingPath, "Show.S01E01.German.1080p-GRP", 100, 250);
        var registration = await AddRegistrationAsync("Main FTP", "main");
        var automation = await AddAutomationAsync(
            registration,
            template,
            a =>
            {
                a.PrimaryLanguageCode = "de";
                a.KeepRawFiles = false;
            }
        );

        // Act
        var pendingCount = await ProcessAsync();

        // Assert
        pendingCount.ShouldBe(0);
        var download = (await GetDownloadsAsync()).Single();
        download.State.ShouldBe(RemoteSourceDownloadState.Observing);
        download.RemoteSourceAutomationId.ShouldBe(automation.Id);
        download.RemoteSourceRegistrationId.ShouldBe(registration.Id);
        download.SourceName.ShouldBe("Main FTP");
        download.RemoteFolderPath.ShouldBe("/incoming/Show.S01E01.German.1080p-GRP");
        download.FolderName.ShouldBe("Show.S01E01.German.1080p-GRP");
        download.LocalFolderPath.ShouldBe(Path.Combine(TargetPath, "Show.S01E01.German.1080p-GRP"));
        download.ReleaseTemplateId.ShouldBe(template.Id);
        download.PrimaryLanguageCode.ShouldBe("de");
        download.KeepRawFiles.ShouldBeFalse();
        download.FileCount.ShouldBe(2);
        download.TotalBytes.ShouldBe(350);
        download.DiscoveredAt.ShouldBe(StartTime);
        download.LastChangedAt.ShouldBe(StartTime);
        download.StartedAt.ShouldBeNull();
        download.CompletedAt.ShouldBeNull();
        download.ReleaseId.ShouldBeNull();
    }

    [Test]
    public async Task ProcessAsync_OverlappingPatternsOnSamePath_LowerPriorityValueClaimsFolder()
    {
        // Arrange
        var germanTemplate = await AddReleaseTemplateAsync("German", ReleaseType.Managed);
        var fallbackTemplate = await AddReleaseTemplateAsync("Fallback", ReleaseType.Unmanaged);
        var server = AddServer("main");
        server.SetFolder(IncomingPath, "Show.S01E01.German.1080p-GRP", 100);
        server.SetFolder(IncomingPath, "Show.S01E01.English.1080p-GRP", 400);
        var registration = await AddRegistrationAsync("Main FTP", "main");
        var catchAll = await AddAutomationAsync(
            registration,
            fallbackTemplate,
            automation =>
            {
                automation.FolderNamePattern = "*";
                automation.Priority = 200;
            }
        );
        var german = await AddAutomationAsync(
            registration,
            germanTemplate,
            automation =>
            {
                automation.FolderNamePattern = "*.German.*";
                automation.Priority = 100;
            }
        );

        // Act
        await ProcessAsync();

        // Assert
        var downloads = await GetDownloadsAsync();
        downloads.Count.ShouldBe(2);

        var germanDownload = downloads.Single(download => download.FolderName.Contains("German"));
        germanDownload.RemoteSourceAutomationId.ShouldBe(german.Id);
        germanDownload.ReleaseTemplateId.ShouldBe(germanTemplate.Id);

        var englishDownload = downloads.Single(download => download.FolderName.Contains("English"));
        englishDownload.RemoteSourceAutomationId.ShouldBe(catchAll.Id);
        englishDownload.ReleaseTemplateId.ShouldBe(fallbackTemplate.Id);
    }

    [Test]
    public async Task ProcessAsync_SeveralAutomationsShareRemotePath_ListsPathOnce()
    {
        // Arrange
        var template = await AddReleaseTemplateAsync("HD", ReleaseType.Managed);
        var server = AddServer("main");
        server.SetFolder(IncomingPath, "Show.S01E01.German.1080p-GRP", 100);
        server.SetFolder("/tv", "Show.S01E02-GRP", 100);
        var registration = await AddRegistrationAsync("Main FTP", "main");
        await AddAutomationAsync(registration, template, a => a.FolderNamePattern = "*German*");
        await AddAutomationAsync(registration, template, a => a.FolderNamePattern = "*720p*");
        await AddAutomationAsync(registration, template, a => a.RemotePath = "/tv");

        // Act
        await ProcessAsync();

        // Assert
        server.ListedPaths.Order(StringComparer.Ordinal).ShouldBe(["/incoming", "/tv"]);
        server.OpenCount.ShouldBe(1);
        (await GetDownloadsAsync()).Count.ShouldBe(2);
    }

    [Test]
    public async Task ProcessAsync_ExistingObservingRecord_KeepsOwnerWhenHigherPriorityAutomationIsAdded()
    {
        // Arrange
        var fallbackTemplate = await AddReleaseTemplateAsync("Fallback", ReleaseType.Unmanaged);
        var germanTemplate = await AddReleaseTemplateAsync("German", ReleaseType.Managed);
        var server = AddServer("main");
        server.SetFolder(IncomingPath, "Show.S01E01.German.1080p-GRP", 100);
        var registration = await AddRegistrationAsync("Main FTP", "main");
        var catchAll = await AddAutomationAsync(
            registration,
            fallbackTemplate,
            automation => automation.Priority = 200
        );
        await ProcessAsync();

        await AddAutomationAsync(
            registration,
            germanTemplate,
            automation =>
            {
                automation.FolderNamePattern = "*.German.*";
                automation.Priority = 100;
            }
        );
        server.SetFolder(IncomingPath, "Show.S01E01.German.1080p-GRP", 100, 200);
        timeProvider.Now = StartTime.AddMinutes(1);

        // Act
        await ProcessAsync();

        // Assert
        var download = (await GetDownloadsAsync()).Single();
        download.RemoteSourceAutomationId.ShouldBe(catchAll.Id);
        download.ReleaseTemplateId.ShouldBe(fallbackTemplate.Id);
        download.State.ShouldBe(RemoteSourceDownloadState.Observing);
        download.TotalBytes.ShouldBe(300);
        download.LastChangedAt.ShouldBe(StartTime.AddMinutes(1));
    }

    [Test]
    public async Task ProcessAsync_OwnerNoLongerMatches_RemovesRecordAndOtherAutomationClaimsLater()
    {
        // Arrange
        var fallbackTemplate = await AddReleaseTemplateAsync("Fallback", ReleaseType.Unmanaged);
        var germanTemplate = await AddReleaseTemplateAsync("German", ReleaseType.Managed);
        var server = AddServer("main");
        server.SetFolder(IncomingPath, "Show.S01E01.German.1080p-GRP", 100);
        var registration = await AddRegistrationAsync("Main FTP", "main");
        var catchAll = await AddAutomationAsync(
            registration,
            fallbackTemplate,
            automation => automation.Priority = 200
        );
        await ProcessAsync();
        var german = await AddAutomationAsync(
            registration,
            germanTemplate,
            automation =>
            {
                automation.FolderNamePattern = "*.German.*";
                automation.Priority = 100;
            }
        );
        await UpdateAutomationAsync(
            catchAll.Id,
            automation => automation.FolderNamePattern = "*.English.*"
        );

        // Act
        await ProcessAsync();
        var afterRemoval = await GetDownloadsAsync();
        await ProcessAsync();

        // Assert
        afterRemoval.ShouldBeEmpty();
        var download = (await GetDownloadsAsync()).Single();
        download.RemoteSourceAutomationId.ShouldBe(german.Id);
        download.ReleaseTemplateId.ShouldBe(germanTemplate.Id);
    }

    [Test]
    public async Task ProcessAsync_NoAutomationMatches_CreatesNoRecord()
    {
        // Arrange
        var template = await AddReleaseTemplateAsync("HD", ReleaseType.Managed);
        var server = AddServer("main");
        server.SetFolder(IncomingPath, "Show.S01E01.720p-GRP", 100);
        var registration = await AddRegistrationAsync("Main FTP", "main");
        await AddAutomationAsync(registration, template, a => a.FolderNamePattern = "*1080p*");

        // Act
        await ProcessAsync();

        // Assert
        (await GetDownloadsAsync()).ShouldBeEmpty();
        server.RecursivelyListedPaths.ShouldBeEmpty();
    }

    [Test]
    public async Task ProcessAsync_IgnoreExistingOnFirstScan_IgnoresCurrentFoldersAndObservesLaterOnes()
    {
        // Arrange
        var template = await AddReleaseTemplateAsync("HD", ReleaseType.Managed);
        var server = AddServer("main");
        server.SetFolder(IncomingPath, "Old.Release.1080p-GRP", 100);
        server.SetFolder(IncomingPath, "Old.Release.720p-GRP", 100);
        var registration = await AddRegistrationAsync("Main FTP", "main");
        var automation = await AddAutomationAsync(
            registration,
            template,
            a =>
            {
                a.FolderNamePattern = "*1080p*";
                a.IgnoreExistingOnFirstScan = true;
            }
        );

        // Act
        await ProcessAsync();
        server.SetFolder(IncomingPath, "New.Release.1080p-GRP", 200);
        timeProvider.Now = StartTime.AddMinutes(2);
        await ProcessAsync();

        // Assert
        var downloads = await GetDownloadsAsync();
        downloads.Count.ShouldBe(2);

        var ignored = downloads.Single(download => download.FolderName == "Old.Release.1080p-GRP");
        ignored.State.ShouldBe(RemoteSourceDownloadState.Ignored);
        ignored.ReleaseTemplateId.ShouldBe(template.Id);

        var observed = downloads.Single(download => download.FolderName == "New.Release.1080p-GRP");
        observed.State.ShouldBe(RemoteSourceDownloadState.Observing);
        observed.TotalBytes.ShouldBe(200);

        server.RecursivelyListedPaths.ShouldBe(["/incoming/New.Release.1080p-GRP"]);
        (await ReloadAutomationAsync(automation.Id)).HasCompletedInitialScan.ShouldBeTrue();
    }

    [Test]
    public async Task ProcessAsync_IgnoreExistingOnlyOnOneAutomation_AppliesToFoldersItClaims()
    {
        // Arrange
        var template = await AddReleaseTemplateAsync("HD", ReleaseType.Managed);
        var server = AddServer("main");
        server.SetFolder(IncomingPath, "Show.German-GRP", 100);
        server.SetFolder(IncomingPath, "Show.English-GRP", 100);
        var registration = await AddRegistrationAsync("Main FTP", "main");
        await AddAutomationAsync(
            registration,
            template,
            automation =>
            {
                automation.FolderNamePattern = "*German*";
                automation.Priority = 100;
                automation.IgnoreExistingOnFirstScan = true;
            }
        );
        await AddAutomationAsync(registration, template, automation => automation.Priority = 200);

        // Act
        await ProcessAsync();

        // Assert
        var downloads = await GetDownloadsAsync();
        downloads
            .Single(download => download.FolderName == "Show.German-GRP")
            .State.ShouldBe(RemoteSourceDownloadState.Ignored);
        downloads
            .Single(download => download.FolderName == "Show.English-GRP")
            .State.ShouldBe(RemoteSourceDownloadState.Observing);
    }

    [Test]
    public async Task ProcessAsync_IgnoreExistingDisabled_ObservesExistingFoldersOnFirstScan()
    {
        // Arrange
        var template = await AddReleaseTemplateAsync("HD", ReleaseType.Managed);
        var server = AddServer("main");
        server.SetFolder(IncomingPath, "Old.Release.1080p-GRP", 100);
        var registration = await AddRegistrationAsync("Main FTP", "main");
        var automation = await AddAutomationAsync(registration, template);

        // Act
        await ProcessAsync();

        // Assert
        (await GetDownloadsAsync())
            .Single()
            .State.ShouldBe(RemoteSourceDownloadState.Observing);
        (await ReloadAutomationAsync(automation.Id)).HasCompletedInitialScan.ShouldBeTrue();
    }

    [Test]
    public async Task ProcessAsync_ObservedFolderChangesAndSettles_BecomesPendingAfterStabilityWindow()
    {
        // Arrange
        var template = await AddReleaseTemplateAsync("HD", ReleaseType.Managed);
        var server = AddServer("main");
        server.SetFolder(IncomingPath, "Release.1080p-GRP", 100);
        var registration = await AddRegistrationAsync("Main FTP", "main");
        await AddAutomationAsync(registration, template);

        // Act
        await ProcessAsync();

        server.SetFolder(IncomingPath, "Release.1080p-GRP", 100, 300);
        timeProvider.Now = StartTime.AddMinutes(1);
        var changedTick = await ProcessAsync();
        var afterChange = (await GetDownloadsAsync()).Single();

        timeProvider.Now = StartTime.AddMinutes(4);
        var settlingTick = await ProcessAsync();
        var afterSettling = (await GetDownloadsAsync()).Single();

        timeProvider.Now = StartTime.AddMinutes(7);
        var stableTick = await ProcessAsync();

        // Assert
        changedTick.ShouldBe(0);
        afterChange.State.ShouldBe(RemoteSourceDownloadState.Observing);
        afterChange.FileCount.ShouldBe(2);
        afterChange.TotalBytes.ShouldBe(400);
        afterChange.LastChangedAt.ShouldBe(StartTime.AddMinutes(1));

        settlingTick.ShouldBe(0);
        afterSettling.State.ShouldBe(RemoteSourceDownloadState.Observing);

        stableTick.ShouldBe(1);
        var pending = (await GetDownloadsAsync()).Single();
        pending.State.ShouldBe(RemoteSourceDownloadState.Pending);
        pending.LastChangedAt.ShouldBe(StartTime.AddMinutes(1));
        pending.DiscoveredAt.ShouldBe(StartTime);
    }

    [Test]
    public async Task ProcessAsync_StableFolderBelowMinimumSize_StaysObserving()
    {
        // Arrange
        remoteSourceConfiguration.MinimumFolderSizeMegabytes = 1;
        var template = await AddReleaseTemplateAsync("HD", ReleaseType.Managed);
        var server = AddServer("main");
        server.SetFolder(IncomingPath, "Tiny.Release-GRP", 1024);
        var registration = await AddRegistrationAsync("Main FTP", "main");
        await AddAutomationAsync(registration, template);

        // Act
        await ProcessAsync();
        timeProvider.Now = StartTime.AddMinutes(30);
        var pendingCount = await ProcessAsync();

        // Assert
        pendingCount.ShouldBe(0);
        (await GetDownloadsAsync()).Single().State.ShouldBe(RemoteSourceDownloadState.Observing);
    }

    [Test]
    public async Task ProcessAsync_StableEmptyFolder_StaysObserving()
    {
        // Arrange
        var template = await AddReleaseTemplateAsync("HD", ReleaseType.Managed);
        var server = AddServer("main");
        server.SetFolder(IncomingPath, "Empty.Release-GRP");
        var registration = await AddRegistrationAsync("Main FTP", "main");
        await AddAutomationAsync(registration, template);

        // Act
        await ProcessAsync();
        timeProvider.Now = StartTime.AddMinutes(30);
        var pendingCount = await ProcessAsync();

        // Assert
        pendingCount.ShouldBe(0);
        (await GetDownloadsAsync()).Single().State.ShouldBe(RemoteSourceDownloadState.Observing);
    }

    [Test]
    public async Task ProcessAsync_ObservedFolderVanishes_RemovesObservingRecord()
    {
        // Arrange
        var template = await AddReleaseTemplateAsync("HD", ReleaseType.Managed);
        var server = AddServer("main");
        server.SetFolder(IncomingPath, "Release.1080p-GRP", 100);
        var registration = await AddRegistrationAsync("Main FTP", "main");
        await AddAutomationAsync(registration, template);

        // Act
        await ProcessAsync();
        (await GetDownloadsAsync()).Count.ShouldBe(1);

        server.RemoveFolder(IncomingPath, "Release.1080p-GRP");
        await ProcessAsync();

        // Assert
        (await GetDownloadsAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task ProcessAsync_ExistingNonObservingRecords_AreNeverTouched()
    {
        // Arrange
        var template = await AddReleaseTemplateAsync("HD", ReleaseType.Managed);
        var server = AddServer("main");
        server.SetFolder(IncomingPath, "Failed.Release-GRP", 999);
        var registration = await AddRegistrationAsync("Main FTP", "main");
        var automation = await AddAutomationAsync(registration, template);
        var failed = await AddDownloadAsync(
            automation,
            "Failed.Release-GRP",
            RemoteSourceDownloadState.Failed
        );
        var downloadedButVanished = await AddDownloadAsync(
            automation,
            "Vanished.Release-GRP",
            RemoteSourceDownloadState.Downloaded
        );

        // Act
        timeProvider.Now = StartTime.AddHours(1);
        await ProcessAsync();

        // Assert
        var downloads = await GetDownloadsAsync();
        downloads.Count.ShouldBe(2);

        var reloadedFailed = downloads.Single(download => download.Id == failed.Id);
        reloadedFailed.State.ShouldBe(RemoteSourceDownloadState.Failed);
        reloadedFailed.FileCount.ShouldBe(1);
        reloadedFailed.TotalBytes.ShouldBe(10);
        reloadedFailed.LastChangedAt.ShouldBe(StartTime);
        reloadedFailed.ErrorMessage.ShouldBe("Previous error");

        downloads
            .Single(download => download.Id == downloadedButVanished.Id)
            .State.ShouldBe(RemoteSourceDownloadState.Downloaded);
        server.RecursivelyListedPaths.ShouldBeEmpty();
    }

    [Test]
    public async Task ProcessAsync_RegistrationCannotBeOpened_OtherRegistrationIsStillScanned()
    {
        // Arrange
        var template = await AddReleaseTemplateAsync("HD", ReleaseType.Managed);
        var brokenServer = AddServer("broken");
        brokenServer.OpenException = new IOException("Login incorrect");
        brokenServer.SetFolder(IncomingPath, "Broken.Release-GRP", 100);
        var workingServer = AddServer("working");
        workingServer.SetFolder(IncomingPath, "Working.Release-GRP", 100);
        var brokenRegistration = await AddRegistrationAsync("Broken FTP", "broken");
        var workingRegistration = await AddRegistrationAsync("Working FTP", "working");
        await AddAutomationAsync(brokenRegistration, template);
        await AddAutomationAsync(workingRegistration, template);

        // Act
        await ProcessAsync();

        // Assert
        (await GetDownloadsAsync())
            .Single()
            .FolderName.ShouldBe("Working.Release-GRP");
    }

    [Test]
    public async Task ProcessAsync_AutomationPathMissing_OtherAutomationOfSameRegistrationIsStillScanned()
    {
        // Arrange
        var template = await AddReleaseTemplateAsync("HD", ReleaseType.Managed);
        var server = AddServer("main");
        server.SetFolder("/tv", "Show.S01E01-GRP", 100);
        var registration = await AddRegistrationAsync("Main FTP", "main");
        var missing = await AddAutomationAsync(
            registration,
            template,
            automation => automation.RemotePath = "/missing"
        );
        await AddAutomationAsync(
            registration,
            template,
            automation => automation.RemotePath = "/tv"
        );

        // Act
        await ProcessAsync();

        // Assert
        (await GetDownloadsAsync())
            .Single()
            .RemoteFolderPath.ShouldBe("/tv/Show.S01E01-GRP");
        (await ReloadAutomationAsync(missing.Id)).HasCompletedInitialScan.ShouldBeFalse();
        server.OpenCount.ShouldBe(1);
        server.DisposeCount.ShouldBe(1);
    }

    [Test]
    public async Task ProcessAsync_UnsafeFolderNames_AreSkipped()
    {
        // Arrange
        var template = await AddReleaseTemplateAsync("HD", ReleaseType.Managed);
        var server = AddServer("main");
        server.SetFolder(IncomingPath, "..", 100);
        server.SetFolder(IncomingPath, "nested\\..\\escape", 100);
        server.SetFolder(IncomingPath, "Safe.Release-GRP", 100);
        var registration = await AddRegistrationAsync("Main FTP", "main");
        await AddAutomationAsync(registration, template);

        // Act
        await ProcessAsync();

        // Assert
        (await GetDownloadsAsync())
            .Single()
            .FolderName.ShouldBe("Safe.Release-GRP");
        server.RecursivelyListedPaths.ShouldBe(["/incoming/Safe.Release-GRP"]);
    }

    [Test]
    public async Task ProcessAsync_DisabledAutomationOrInactiveRegistration_IsNotScanned()
    {
        // Arrange
        var template = await AddReleaseTemplateAsync("HD", ReleaseType.Managed);
        var activeServer = AddServer("active");
        activeServer.SetFolder(IncomingPath, "Release-GRP", 100);
        var inactiveServer = AddServer("inactive");
        inactiveServer.SetFolder(IncomingPath, "Release-GRP", 100);
        var activeRegistration = await AddRegistrationAsync("Active FTP", "active");
        var inactiveRegistration = await AddRegistrationAsync(
            "Inactive FTP",
            "inactive",
            isActive: false
        );
        await AddAutomationAsync(
            activeRegistration,
            template,
            automation => automation.IsEnabled = false
        );
        await AddAutomationAsync(inactiveRegistration, template);

        // Act
        await ProcessAsync();

        // Assert
        (await GetDownloadsAsync()).ShouldBeEmpty();
        activeServer.OpenCount.ShouldBe(0);
        inactiveServer.OpenCount.ShouldBe(0);
    }

    private async Task<int> ProcessAsync()
    {
        var dbContext = CreateDbContext();
        var repository = new RemoteSourceAutomationRepository(dbContext, dbContext);
        var factory = new Mock<IRemoteSourceFactory>(MockBehavior.Strict);
        factory.Setup(f => f.GetByClassName(SourceClassName)).Returns(remoteSource);
        var secretProtector = new Mock<ISecretProtector>(MockBehavior.Strict);
        secretProtector
            .Setup(p => p.Unprotect(It.IsAny<string>()))
            .Returns((string value) => value);

        var service = new RemoteSourceScanService(
            repository,
            new RemoteSourceSessionOpener(factory.Object, secretProtector.Object),
            timeProvider,
            CreateConfigurationProvider(),
            NullLogger<RemoteSourceScanService>.Instance
        );

        return await service.ProcessAsync(CancellationToken.None);
    }

    private IApplicationConfigurationProvider CreateConfigurationProvider()
    {
        var configurationProvider = new Mock<IApplicationConfigurationProvider>(
            MockBehavior.Strict
        );
        configurationProvider
            .Setup(provider =>
                provider.GetValue(It.IsAny<Expression<Func<RemoteSourceConfiguration, int>>>())
            )
            .Returns(
                (Expression<Func<RemoteSourceConfiguration, int>> selector) =>
                    selector.Compile()(remoteSourceConfiguration)
            );

        return configurationProvider.Object;
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

    private async Task<RemoteSourceAutomation> ReloadAutomationAsync(int id)
    {
        return await CreateDbContext().RemoteSourceAutomations.SingleAsync(a => a.Id == id);
    }

    private async Task<ReleaseTemplate> AddReleaseTemplateAsync(
        string name,
        ReleaseType releaseType
    )
    {
        var template = new ReleaseTemplate
        {
            Name = name,
            ReleaseType = releaseType,
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{name} group",
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
        string name,
        string serverKey,
        bool isActive = true
    )
    {
        var registration = new RemoteSourceRegistration
        {
            Name = name,
            SerializedConfig = serverKey,
            SourceClassName = SourceClassName,
            IsActive = isActive,
        };

        var dbContext = CreateDbContext();
        dbContext.RemoteSourceRegistrations.Add(registration);
        await dbContext.SaveChangesAsync();

        return registration;
    }

    private async Task<RemoteSourceAutomation> AddAutomationAsync(
        RemoteSourceRegistration registration,
        ReleaseTemplate template,
        Action<RemoteSourceAutomation>? configure = null
    )
    {
        var automation = new RemoteSourceAutomation
        {
            Name = "Automation",
            RemoteSourceRegistrationId = registration.Id,
            RemotePath = IncomingPath,
            TargetPath = TargetPath,
            ReleaseTemplateId = template.Id,
            IsEnabled = true,
        };
        configure?.Invoke(automation);

        var dbContext = CreateDbContext();
        dbContext.RemoteSourceAutomations.Add(automation);
        await dbContext.SaveChangesAsync();

        return automation;
    }

    private async Task UpdateAutomationAsync(int id, Action<RemoteSourceAutomation> update)
    {
        var dbContext = CreateDbContext();
        var automation = await dbContext.RemoteSourceAutomations.SingleAsync(a => a.Id == id);
        update(automation);
        await dbContext.SaveChangesAsync();
    }

    private async Task<RemoteSourceDownload> AddDownloadAsync(
        RemoteSourceAutomation automation,
        string folderName,
        RemoteSourceDownloadState state
    )
    {
        var download = new RemoteSourceDownload
        {
            RemoteSourceAutomationId = automation.Id,
            RemoteSourceRegistrationId = automation.RemoteSourceRegistrationId,
            SourceName = "Main FTP",
            RemoteFolderPath = FakeRemoteServer.CombinePath(automation.RemotePath, folderName),
            FolderName = folderName,
            LocalFolderPath = Path.Combine(TargetPath, folderName),
            State = state,
            FileCount = 1,
            TotalBytes = 10,
            LastChangedAt = StartTime,
            DiscoveredAt = StartTime,
            ErrorMessage = "Previous error",
        };

        var dbContext = CreateDbContext();
        dbContext.RemoteSourceDownloads.Add(download);
        await dbContext.SaveChangesAsync();

        return download;
    }
}
