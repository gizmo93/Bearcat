using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database;

public class ConcurrentDatabaseWritesTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const int WriterCount = 16;
    private const int NotificationsPerWriter = 5;

    private static readonly DateTime CreatedAt = new(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ResolvedAt = new(2026, 10, 2, 1, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task SaveChangesAsync_ManyContextsInsertAndUpdateConcurrently_PersistsAllWrites()
    {
        // Arrange
        var startSignal = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var writerTasks = Enumerable
            .Range(0, WriterCount)
            .Select(writerIndex =>
                InsertAndResolveNotificationsAsync(writerIndex, startSignal.Task)
            )
            .ToList();

        // Act
        startSignal.SetResult();
        await Task.WhenAll(writerTasks);

        // Assert
        var notifications = await ReadDbContext
            .Notifications.Select(notification => new
            {
                notification.Message,
                notification.ResolvedAt,
            })
            .ToListAsync();
        notifications.ShouldAllBe(notification => notification.ResolvedAt == ResolvedAt);
        notifications
            .Select(notification => notification.Message)
            .ShouldBe(
                Enumerable
                    .Range(0, WriterCount)
                    .SelectMany(writerIndex =>
                        Enumerable
                            .Range(0, NotificationsPerWriter)
                            .Select(notificationIndex =>
                                CreateResolvedMessage(writerIndex, notificationIndex)
                            )
                    ),
                ignoreOrder: true
            );
    }

    private async Task InsertAndResolveNotificationsAsync(int writerIndex, Task startSignal)
    {
        await using var dbContext = Database.CreateDbContext();
        await startSignal;

        var notifications = new List<Notification>();
        for (
            var notificationIndex = 0;
            notificationIndex < NotificationsPerWriter;
            notificationIndex++
        )
        {
            var notification = new Notification
            {
                CreatedAt = CreatedAt,
                NotificationSeverity = NotificationSeverity.Info,
                NotificationKind = NotificationKind.Legacy,
                Message = CreateMessage(writerIndex, notificationIndex),
            };
            dbContext.Notifications.Add(notification);
            await dbContext.SaveChangesAsync();
            notifications.Add(notification);
        }

        for (
            var notificationIndex = 0;
            notificationIndex < NotificationsPerWriter;
            notificationIndex++
        )
        {
            notifications[notificationIndex].ResolvedAt = ResolvedAt;
            notifications[notificationIndex].Message = CreateResolvedMessage(
                writerIndex,
                notificationIndex
            );
            await dbContext.SaveChangesAsync();
        }
    }

    private static string CreateMessage(int writerIndex, int notificationIndex) =>
        $"Writer {writerIndex:D2} notification {notificationIndex}";

    private static string CreateResolvedMessage(int writerIndex, int notificationIndex) =>
        $"Writer {writerIndex:D2} notification {notificationIndex} resolved";
}
