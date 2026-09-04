using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageNotifications.Dto;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageNotifications;

public class NotificationReadRepositoryTest : BearcatIntegrationTest
{
    [TestCase(null, false, 15)]
    [TestCase(null, true, 16)]
    [TestCase(NotificationKind.UploadFailed, false, 7)]
    [TestCase(NotificationKind.UploadFailed, true, 8)]
    [TestCase(NotificationKind.Legacy, false, 1)]
    [TestCase(NotificationKind.CaptchaVerificationRequired, false, 0)]
    public async Task Search_KindAndResolvedFilters_ReturnMatchingNotificationsAndCount(
        NotificationKind? kind,
        bool includeResolved,
        int expectedCount
    )
    {
        await using var dbContext = Database.CreateDbContext();
        await SeedAsync(dbContext);
        var repository = new NotificationReadRepository(dbContext);

        var result = await repository.SearchAsync(
            new NotificationSearchQuery(
                PageSize: 100,
                IncludeResolved: includeResolved,
                NotificationKind: kind
            )
        );

        result.TotalCount.ShouldBe(expectedCount);
        result.Items.Count.ShouldBe(expectedCount);

        if (kind is not null)
        {
            result.Items.ShouldAllBe(notification => notification.NotificationKind == kind);
        }

        if (!includeResolved)
        {
            result.Items.ShouldAllBe(notification => notification.ResolvedAt == null);
        }
    }

    [Test]
    public async Task Search_KindFilterWithSecondPage_AppliesFilterBeforePagination()
    {
        await using var dbContext = Database.CreateDbContext();
        await SeedAsync(dbContext);
        var repository = new NotificationReadRepository(dbContext);

        var result = await repository.SearchAsync(
            new NotificationSearchQuery(
                PageIndex: 1,
                PageSize: 5,
                NotificationKind: NotificationKind.UploadFailed
            )
        );

        result.TotalCount.ShouldBe(7);
        result.TotalPages.ShouldBe(2);
        result.PageIndex.ShouldBe(1);
        result
            .Items.Select(notification => notification.Message)
            .ShouldBe(["Upload 1", "Upload 0"]);
    }

    private static async Task SeedAsync(BearcatDbContext dbContext)
    {
        var createdAt = new DateTime(
            year: 2026,
            month: 9,
            day: 4,
            hour: 0,
            minute: 0,
            second: 0,
            kind: DateTimeKind.Unspecified
        );

        for (var index = 0; index < 7; index++)
        {
            dbContext.Notifications.AddRange(
                new Notification
                {
                    CreatedAt = createdAt.AddMinutes(index),
                    NotificationKind = NotificationKind.UploadFailed,
                    NotificationSeverity = NotificationSeverity.Error,
                    Message = $"Upload {index}",
                },
                new Notification
                {
                    CreatedAt = createdAt.AddMinutes(index),
                    NotificationKind = NotificationKind.ArchiveCreationFailed,
                    NotificationSeverity = NotificationSeverity.Error,
                    Message = $"Archive {index}",
                }
            );
        }

        dbContext.Notifications.AddRange(
            new Notification
            {
                CreatedAt = createdAt,
                NotificationKind = NotificationKind.Legacy,
                NotificationSeverity = NotificationSeverity.Warning,
                Message = "Old notification",
            },
            new Notification
            {
                CreatedAt = createdAt.AddHours(1),
                ResolvedAt = createdAt.AddHours(2),
                NotificationKind = NotificationKind.UploadFailed,
                NotificationSeverity = NotificationSeverity.Error,
                Message = "Resolved upload",
            }
        );

        await dbContext.SaveChangesAsync();
    }
}
