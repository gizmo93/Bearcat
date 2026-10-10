using Bearcat.Domain.Entities;
using Bearcat.Domain.IntegrationTest.Shared.FolderConfirmation;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.FolderConfirmation;
using Bearcat.Domain.UseCases.ManageConfirmedFolders;
using Bearcat.Domain.UseCases.ManageConfirmedFolders.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageConfirmedFolders;

public class ConfirmedFolderServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private string tempRootPath = null!;
    private string workingDirectoryPath = null!;
    private string otherWorkingDirectoryPath = null!;
    private string missingWorkingDirectoryPath = null!;
    private ConfirmedFolderService service = null!;

    [SetUp]
    public void Setup()
    {
        tempRootPath = Path.Combine(Path.GetTempPath(), $"bearcat-tests-{Guid.NewGuid():N}");
        workingDirectoryPath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, "data"))
            .FullName;
        otherWorkingDirectoryPath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, "other"))
            .FullName;
        missingWorkingDirectoryPath = Path.Combine(tempRootPath, "missing");

        var workingDirectoriesConfig = new WorkingDirectoriesConfig
        {
            WorkingDirectories =
            [
                workingDirectoryPath,
                otherWorkingDirectoryPath,
                missingWorkingDirectoryPath,
                "",
            ],
        };
        var rootProvider = new ConfirmableFolderRootProvider(
            Options.Create(workingDirectoriesConfig)
        );

        service = new ConfirmedFolderService(
            rootProvider,
            FolderConfirmationTestFactory.CreateCheck(DbContext, workingDirectoriesConfig),
            new ConfirmedFolderRepository(DbContext, DbContext),
            new FileSystemService(),
            CreateTimeProvider(),
            NullLogger<ConfirmedFolderService>.Instance
        );
    }

    [TearDown]
    public void DeleteTempRootPath()
    {
        if (Directory.Exists(tempRootPath))
        {
            Directory.Delete(tempRootPath, recursive: true);
        }
    }

    [Test]
    public async Task ConfirmFolderAsync_NotConfirmedWorkingDirectory_WritesMarkerFileAndStoresConfirmation()
    {
        // Act
        await service.ConfirmFolderAsync(workingDirectoryPath, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var confirmedFolder = await DbContext.ConfirmedFolders.SingleAsync();
        confirmedFolder.Path.ShouldBe(workingDirectoryPath);
        var markerFileContent = await File.ReadAllTextAsync(
            FolderConfirmationMarkerFile.GetFilePath(workingDirectoryPath)
        );
        Guid.Parse(markerFileContent).ShouldBe(confirmedFolder.MarkerId);
    }

    [Test]
    public async Task ConfirmFolderAsync_AlreadyConfirmedWorkingDirectory_ReplacesMarker()
    {
        // Arrange
        await service.ConfirmFolderAsync(workingDirectoryPath, CancellationToken.None);
        DbContext.ChangeTracker.Clear();
        var previousMarkerId = (await DbContext.ConfirmedFolders.SingleAsync()).MarkerId;
        File.Delete(FolderConfirmationMarkerFile.GetFilePath(workingDirectoryPath));

        // Act
        await service.ConfirmFolderAsync(workingDirectoryPath, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var confirmedFolder = await DbContext.ConfirmedFolders.SingleAsync();
        confirmedFolder.MarkerId.ShouldNotBe(previousMarkerId);
        Guid.Parse(
                await File.ReadAllTextAsync(
                    FolderConfirmationMarkerFile.GetFilePath(workingDirectoryPath)
                )
            )
            .ShouldBe(confirmedFolder.MarkerId);
    }

    [Test]
    public async Task ConfirmFolderAsync_MissingWorkingDirectory_ThrowsWithoutCreatingIt()
    {
        // Act
        await Should.ThrowAsync<InvalidOperationException>(() =>
            service.ConfirmFolderAsync(missingWorkingDirectoryPath, CancellationToken.None)
        );

        // Assert
        Directory.Exists(missingWorkingDirectoryPath).ShouldBeFalse();
        (await DbContext.ConfirmedFolders.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task ConfirmFolderAsync_PathIsNoWorkingDirectory_Throws()
    {
        // Arrange
        var subfolderPath = Directory
            .CreateDirectory(Path.Combine(workingDirectoryPath, "release"))
            .FullName;

        // Act
        await Should.ThrowAsync<InvalidOperationException>(() =>
            service.ConfirmFolderAsync(subfolderPath, CancellationToken.None)
        );

        // Assert
        File.Exists(FolderConfirmationMarkerFile.GetFilePath(subfolderPath)).ShouldBeFalse();
        (await DbContext.ConfirmedFolders.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task ConfirmFolderAsync_UnresolvedNotifications_ResolvesOnlyNotificationsOfThatWorkingDirectory()
    {
        // Arrange
        var notConfirmedNotification = AddNotification(
            FolderNotConfirmedNotificationMessage.Get(
                new FolderConfirmationResult(
                    FolderConfirmationState.NotConfirmed,
                    workingDirectoryPath
                )
            )
        );
        var markerMissingNotification = AddNotification(
            FolderNotConfirmedNotificationMessage.Get(
                new FolderConfirmationResult(
                    FolderConfirmationState.MarkerMissing,
                    workingDirectoryPath
                )
            )
        );
        var otherWorkingDirectoryNotification = AddNotification(
            FolderNotConfirmedNotificationMessage.Get(
                new FolderConfirmationResult(
                    FolderConfirmationState.NotConfirmed,
                    otherWorkingDirectoryPath
                )
            )
        );
        await DbContext.SaveChangesAsync();

        // Act
        await service.ConfirmFolderAsync(workingDirectoryPath, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var resolvedAtById = await DbContext.Notifications.ToDictionaryAsync(
            notification => notification.Id,
            notification => notification.ResolvedAt
        );
        resolvedAtById[notConfirmedNotification.Id].ShouldNotBeNull();
        resolvedAtById[markerMissingNotification.Id].ShouldNotBeNull();
        resolvedAtById[otherWorkingDirectoryNotification.Id].ShouldBeNull();
    }

    [Test]
    public async Task GetConfirmableFoldersAsync_MixedWorkingDirectories_ReturnsStateAndExistence()
    {
        // Arrange
        await service.ConfirmFolderAsync(workingDirectoryPath, CancellationToken.None);
        await service.ConfirmFolderAsync(otherWorkingDirectoryPath, CancellationToken.None);
        File.Delete(FolderConfirmationMarkerFile.GetFilePath(otherWorkingDirectoryPath));

        // Act
        var result = await service.GetConfirmableFoldersAsync(CancellationToken.None);

        // Assert
        result.ShouldBe([
            new ConfirmableFolderReadModel(
                workingDirectoryPath,
                FolderConfirmationState.Confirmed,
                Exists: true
            ),
            new ConfirmableFolderReadModel(
                otherWorkingDirectoryPath,
                FolderConfirmationState.MarkerMissing,
                Exists: true
            ),
            new ConfirmableFolderReadModel(
                missingWorkingDirectoryPath,
                FolderConfirmationState.NotConfirmed,
                Exists: false
            ),
        ]);
    }

    [Test]
    public void GetTopLevelEntries_ManyEntries_ReturnsFoldersFirstAndCapsEntries()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(workingDirectoryPath, "b-folder"));
        Directory.CreateDirectory(Path.Combine(workingDirectoryPath, "a-folder", "nested"));
        foreach (var index in Enumerable.Range(1, ConfirmedFolderService.MaxTopLevelEntryCount))
        {
            File.WriteAllText(Path.Combine(workingDirectoryPath, $"file-{index:D3}.txt"), "x");
        }

        // Act
        var result = service.GetTopLevelEntries(workingDirectoryPath);

        // Assert
        result.TotalEntryCount.ShouldBe(ConfirmedFolderService.MaxTopLevelEntryCount + 2);
        result.Entries.Count.ShouldBe(ConfirmedFolderService.MaxTopLevelEntryCount);
        result.Entries[0].ShouldBe(new FolderTopLevelEntryReadModel("a-folder", IsFolder: true));
        result.Entries[1].ShouldBe(new FolderTopLevelEntryReadModel("b-folder", IsFolder: true));
        result
            .Entries[2]
            .ShouldBe(new FolderTopLevelEntryReadModel("file-001.txt", IsFolder: false));
    }

    [Test]
    public void GetTopLevelEntries_MissingWorkingDirectory_ReturnsNoEntries()
    {
        // Act
        var result = service.GetTopLevelEntries(missingWorkingDirectoryPath);

        // Assert
        result.Entries.ShouldBeEmpty();
        result.TotalEntryCount.ShouldBe(0);
    }

    private Notification AddNotification(string message)
    {
        var notification = new Notification
        {
            NotificationKind = NotificationKind.FolderNotConfirmed,
            NotificationSeverity = NotificationSeverity.Error,
            Message = message,
            CreatedAt = DateTime.UtcNow,
        };
        DbContext.Notifications.Add(notification);

        return notification;
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }
}
