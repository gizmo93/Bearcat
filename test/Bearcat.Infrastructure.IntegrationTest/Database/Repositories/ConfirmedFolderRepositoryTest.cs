using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ConfirmedFolderRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string FolderPath = "/mnt/data";
    private const string Message = "The folder /mnt/data must be confirmed.";

    private static readonly DateTime ConfirmedAt = new(
        2026,
        10,
        10,
        12,
        0,
        0,
        DateTimeKind.Unspecified
    );

    [Test]
    public async Task GetMarkerIdAsync_ConfirmedFolderExists_ReturnsMarkerId()
    {
        // Arrange
        var markerId = Guid.NewGuid();
        await AddConfirmedFolderAsync(FolderPath, markerId);
        await AddConfirmedFolderAsync("/mnt/other", Guid.NewGuid());

        // Act
        var result = await CreateRepository().GetMarkerIdAsync(FolderPath, CancellationToken.None);

        // Assert
        result.ShouldBe(markerId);
    }

    [Test]
    public async Task GetMarkerIdAsync_NoConfirmedFolderForPath_ReturnsNull()
    {
        // Arrange
        await AddConfirmedFolderAsync("/mnt/data/nested", Guid.NewGuid());

        // Act
        var result = await CreateRepository().GetMarkerIdAsync(FolderPath, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task AnyUnresolvedFolderNotConfirmedNotificationAsync_UnresolvedNotificationWithSameMessage_ReturnsTrue()
    {
        // Arrange
        await AddNotificationAsync(NotificationKind.FolderNotConfirmed, Message, resolvedAt: null);

        // Act
        var result = await CreateRepository()
            .AnyUnresolvedFolderNotConfirmedNotificationAsync(Message, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public async Task AnyUnresolvedFolderNotConfirmedNotificationAsync_OnlyResolvedOrOtherNotifications_ReturnsFalse()
    {
        // Arrange
        await AddNotificationAsync(NotificationKind.FolderNotConfirmed, Message, ConfirmedAt);
        await AddNotificationAsync(
            NotificationKind.RemoteDownloadFailed,
            Message,
            resolvedAt: null
        );
        await AddNotificationAsync(
            NotificationKind.FolderNotConfirmed,
            "The folder /mnt/other must be confirmed.",
            resolvedAt: null
        );

        // Act
        var result = await CreateRepository()
            .AnyUnresolvedFolderNotConfirmedNotificationAsync(Message, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public async Task GetForUpdateAsync_ConfirmedFolderExists_ReturnsTrackedEntity()
    {
        // Arrange
        await AddConfirmedFolderAsync(FolderPath, Guid.NewGuid());
        var repository = CreateRepository();
        var newMarkerId = Guid.NewGuid();

        // Act
        var result = await repository.GetForUpdateAsync(FolderPath, CancellationToken.None);
        result!.MarkerId = newMarkerId;
        await repository.SaveChangesAsync(CancellationToken.None);

        // Assert
        (await CreateDbContext().ConfirmedFolders.SingleAsync()).MarkerId.ShouldBe(newMarkerId);
    }

    [Test]
    public async Task GetForUpdateAsync_NoConfirmedFolderForPath_ReturnsNull()
    {
        // Act
        var result = await CreateRepository().GetForUpdateAsync(FolderPath, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task Add_NewConfirmedFolder_PersistsAllProperties()
    {
        // Arrange
        var repository = CreateRepository();
        var markerId = Guid.NewGuid();

        // Act
        repository.Add(
            new ConfirmedFolder
            {
                Path = FolderPath,
                MarkerId = markerId,
                ConfirmedAt = ConfirmedAt,
            }
        );
        await repository.SaveChangesAsync(CancellationToken.None);

        // Assert
        var result = await CreateDbContext().ConfirmedFolders.SingleAsync();
        result.Path.ShouldBe(FolderPath);
        result.MarkerId.ShouldBe(markerId);
        result.ConfirmedAt.ShouldBe(ConfirmedAt);
    }

    [Test]
    public async Task Add_PathAlreadyConfirmed_ViolatesUniqueIndex()
    {
        // Arrange
        await AddConfirmedFolderAsync(FolderPath, Guid.NewGuid());
        var repository = CreateRepository();
        repository.Add(
            new ConfirmedFolder
            {
                Path = FolderPath,
                MarkerId = Guid.NewGuid(),
                ConfirmedAt = ConfirmedAt,
            }
        );

        // Act
        var action = () => repository.SaveChangesAsync(CancellationToken.None);

        // Assert
        await action.ShouldThrowAsync<DbUpdateException>();
    }

    [Test]
    public async Task ResolveFolderNotConfirmedNotificationsAsync_MatchingAndOtherNotifications_ResolvesOnlyMatchingUnresolvedNotifications()
    {
        // Arrange
        const string markerMissingMessage =
            "The folder /mnt/data was confirmed, but its marker file is missing.";
        var notConfirmed = await AddNotificationAsync(
            NotificationKind.FolderNotConfirmed,
            Message,
            resolvedAt: null
        );
        var markerMissing = await AddNotificationAsync(
            NotificationKind.FolderNotConfirmed,
            markerMissingMessage,
            resolvedAt: null
        );
        var otherFolder = await AddNotificationAsync(
            NotificationKind.FolderNotConfirmed,
            "The folder /mnt/other must be confirmed.",
            resolvedAt: null
        );
        var otherKind = await AddNotificationAsync(
            NotificationKind.RemoteDownloadFailed,
            Message,
            resolvedAt: null
        );
        var alreadyResolvedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var alreadyResolved = await AddNotificationAsync(
            NotificationKind.FolderNotConfirmed,
            Message,
            alreadyResolvedAt
        );

        // Act
        await CreateRepository()
            .ResolveFolderNotConfirmedNotificationsAsync(
                [Message, markerMissingMessage],
                ConfirmedAt,
                CancellationToken.None
            );

        // Assert
        var resolvedAtById = await CreateDbContext()
            .Notifications.ToDictionaryAsync(
                notification => notification.Id,
                notification => notification.ResolvedAt
            );
        resolvedAtById[notConfirmed.Id].ShouldBe(ConfirmedAt);
        resolvedAtById[markerMissing.Id].ShouldBe(ConfirmedAt);
        resolvedAtById[otherFolder.Id].ShouldBeNull();
        resolvedAtById[otherKind.Id].ShouldBeNull();
        resolvedAtById[alreadyResolved.Id].ShouldBe(alreadyResolvedAt);
    }

    private ConfirmedFolderRepository CreateRepository()
    {
        var dbContext = CreateDbContext();

        return new ConfirmedFolderRepository(ReadDbContext, dbContext);
    }

    private async Task AddConfirmedFolderAsync(string path, Guid markerId)
    {
        DbContext.ConfirmedFolders.Add(
            new ConfirmedFolder
            {
                Path = path,
                MarkerId = markerId,
                ConfirmedAt = ConfirmedAt,
            }
        );
        await DbContext.SaveChangesAsync();
    }

    private async Task<Notification> AddNotificationAsync(
        NotificationKind kind,
        string message,
        DateTime? resolvedAt
    )
    {
        var notification = new Notification
        {
            NotificationKind = kind,
            NotificationSeverity = NotificationSeverity.Error,
            Message = message,
            CreatedAt = ConfirmedAt,
            ResolvedAt = resolvedAt,
        };
        DbContext.Notifications.Add(notification);
        await DbContext.SaveChangesAsync();

        return notification;
    }
}
