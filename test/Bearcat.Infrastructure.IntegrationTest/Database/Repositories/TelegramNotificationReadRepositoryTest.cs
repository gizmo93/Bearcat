using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageNotifications.Telegram;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class TelegramNotificationReadRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const int MaxAttempts = 3;

    private TelegramNotificationReadRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        var readDbContext = CreateDbContext();
        readDbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        repository = new TelegramNotificationReadRepository(readDbContext);
    }

    [Test]
    public async Task GetDeliveryStatusAsync_NoDeliveries_ReturnsEmptyStatus()
    {
        // Act
        var result = await repository.GetDeliveryStatusAsync(MaxAttempts, CancellationToken.None);

        // Assert
        result.ShouldBe(new TelegramDeliveryStatus(0, 0, null, null));
    }

    [Test]
    public async Task GetDeliveryStatusAsync_OnlyUndeliveredDeliveries_ReturnsNoLastDeliveredAt()
    {
        // Arrange
        AddDelivery(deliveredAt: null, attemptCount: 1, lastError: "Timeout");
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetDeliveryStatusAsync(MaxAttempts, CancellationToken.None);

        // Assert
        result.ShouldBe(new TelegramDeliveryStatus(1, 0, null, "Timeout"));
    }

    [Test]
    public async Task GetDeliveryStatusAsync_MixedDeliveries_ReturnsCountsLatestDeliveryAndLatestUndeliveredError()
    {
        // Arrange
        AddDelivery(
            deliveredAt: new DateTime(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc),
            attemptCount: 1
        );
        AddDelivery(
            deliveredAt: new DateTime(2026, 9, 4, 10, 0, 0, 500, DateTimeKind.Utc),
            attemptCount: 2
        );
        AddDelivery(
            deliveredAt: new DateTime(2026, 9, 4, 9, 59, 59, 999, DateTimeKind.Utc),
            attemptCount: 1
        );
        AddDelivery(
            deliveredAt: new DateTime(2026, 9, 3, 23, 59, 59, 999, DateTimeKind.Utc),
            attemptCount: 1
        );
        AddDelivery(deliveredAt: null, attemptCount: 0);
        AddDelivery(deliveredAt: null, attemptCount: 2, lastError: "First error");
        await DbContext.SaveChangesAsync();
        AddDelivery(
            deliveredAt: null,
            attemptCount: 3,
            lastError: "Sending to Café channel failed"
        );
        AddDelivery(deliveredAt: null, attemptCount: 5);
        AddDelivery(
            deliveredAt: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            attemptCount: 2,
            lastError: "Error before successful retry"
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetDeliveryStatusAsync(MaxAttempts, CancellationToken.None);

        // Assert
        result.ShouldBe(
            new TelegramDeliveryStatus(
                PendingCount: 2,
                FailedCount: 2,
                LastDeliveredAt: new DateTime(2026, 9, 4, 10, 0, 0, 500, DateTimeKind.Utc),
                LastError: "Sending to Café channel failed"
            )
        );
    }

    private void AddDelivery(DateTime? deliveredAt, int attemptCount, string? lastError = null)
    {
        DbContext.TelegramDeliveries.Add(
            new TelegramDelivery
            {
                Notification = new Notification
                {
                    CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                    NotificationKind = NotificationKind.UploadFailed,
                    NotificationSeverity = NotificationSeverity.Error,
                    Message = "Upload failed",
                },
                CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                DeliveredAt = deliveredAt,
                AttemptCount = attemptCount,
                LastError = lastError,
            }
        );
    }
}
