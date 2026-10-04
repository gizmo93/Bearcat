using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageNotifications.Dto;
using Bearcat.Domain.UseCases.ManageNotifications.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class NotificationReadRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
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

    [Test]
    public async Task CountUnresolvedAsync_ResolvedAndUnresolvedNotifications_CountsOnlyUnresolved()
    {
        // Arrange
        await using var dbContext = Database.CreateDbContext();
        await SeedAsync(dbContext);
        var repository = new NotificationReadRepository(dbContext);

        // Act
        var result = await repository.CountUnresolvedAsync(CancellationToken.None);

        // Assert
        result.ShouldBe(15);
    }

    [Test]
    public async Task GetLatestUnresolvedAsync_CloseTimestamps_ReturnsNewestUnresolvedByCreatedAtThenId()
    {
        // Arrange
        await using var dbContext = Database.CreateDbContext();
        dbContext.Notifications.AddRange(
            CreateNotification(
                "Oldest",
                new DateTime(2026, 9, 4, 9, 59, 59, 999, DateTimeKind.Utc)
            ),
            CreateNotification("First", new DateTime(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc)),
            CreateNotification("Second", new DateTime(2026, 9, 4, 10, 0, 0, 500, DateTimeKind.Utc)),
            CreateNotification(
                "Resolved",
                new DateTime(2026, 9, 4, 11, 0, 0, DateTimeKind.Utc),
                resolvedAt: new DateTime(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc)
            )
        );
        await dbContext.SaveChangesAsync();
        dbContext.Notifications.Add(
            CreateNotification("Third", new DateTime(2026, 9, 4, 10, 0, 0, 500, DateTimeKind.Utc))
        );
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var repository = new NotificationReadRepository(dbContext);

        // Act
        var result = await repository.GetLatestUnresolvedAsync(3, CancellationToken.None);

        // Assert
        result.Select(notification => notification.Message).ShouldBe(["Third", "Second", "First"]);
        result[1].CreatedAt.ShouldBe(new DateTime(2026, 9, 4, 10, 0, 0, 500, DateTimeKind.Utc));
        result.ShouldAllBe(notification => notification.ResolvedAt == null);
    }

    [Test]
    public async Task GetByIdAsync_NotificationLinkedToUpload_ReturnsUploadAsRelatedEntity()
    {
        // Arrange
        await using var dbContext = Database.CreateDbContext();
        var uploadConfig = CreateUploadConfig("Bearcat.Release.001");
        var notification = CreateNotification(
            "Upload failed",
            new DateTime(2026, 9, 4, 23, 59, 59, 999, DateTimeKind.Utc),
            resolvedAt: new DateTime(2026, 9, 5, 0, 0, 0, 1, DateTimeKind.Utc)
        );
        notification.Upload = new Upload
        {
            UploadConfig = uploadConfig,
            CreatedAt = DateTime.UtcNow,
            UploadState = UploadState.Failed,
            OnlineState = OnlineState.Unknown,
        };
        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var repository = new NotificationReadRepository(dbContext);

        // Act
        var result = await repository.GetByIdAsync(notification.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.NotificationId.ShouldBe(notification.Id);
        result.CreatedAt.ShouldBe(new DateTime(2026, 9, 4, 23, 59, 59, 999, DateTimeKind.Utc));
        result.ResolvedAt.ShouldBe(new DateTime(2026, 9, 5, 0, 0, 0, 1, DateTimeKind.Utc));
        result.Message.ShouldBe("Upload failed");
        result.RelatedEntity.ShouldBe(
            new NotificationRelatedEntityReadModel(
                EntityType: "Upload",
                DisplayName: "Bearcat.Release.001 / Main upload",
                TargetUrl: $"/releases/{uploadConfig.ReleaseId}?tab=uploads&view=history&uploadConfigId={uploadConfig.Id}"
            )
        );
    }

    [Test]
    public async Task GetByIdAsync_NotificationLinkedToArchive_ReturnsArchiveAsRelatedEntity()
    {
        // Arrange
        await using var dbContext = Database.CreateDbContext();
        var uploadConfig = CreateUploadConfig("Bearcat.Release.001");
        dbContext.UploadConfigs.Add(uploadConfig);
        var notification = CreateNotification("Archive failed", DateTime.UtcNow);
        notification.Archive = new Archive
        {
            ArchiveConfig = uploadConfig.ArchiveConfig,
            ArchiveFolderPath = "/tmp/archive/Bearcat.Release.001",
            CreatedAt = DateTime.UtcNow,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
        };
        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var repository = new NotificationReadRepository(dbContext);

        // Act
        var result = await repository.GetByIdAsync(notification.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.RelatedEntity.ShouldBe(
            new NotificationRelatedEntityReadModel(
                EntityType: "Archive",
                DisplayName: "Bearcat.Release.001 / Main archive",
                TargetUrl: $"/releases/{uploadConfig.ReleaseId}?tab=archives&archiveConfigId={uploadConfig.ArchiveConfigId}"
            )
        );
    }

    [Test]
    public async Task GetByIdAsync_NotificationLinkedToReleaseLinkCrypterContainer_ReturnsContainerAsRelatedEntity()
    {
        // Arrange
        await using var dbContext = Database.CreateDbContext();
        var uploadConfig = CreateUploadConfig("Bearcat.Release.001");
        var linkCrypterRegistration = CreateLinkCrypterRegistration();
        var notification = CreateNotification("Container failed", DateTime.UtcNow);
        notification.LinkCrypterContainer = new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.Release,
            UploadConfigLinkCrypter = new UploadConfigLinkCrypter
            {
                UploadConfig = uploadConfig,
                LinkCrypterRegistration = linkCrypterRegistration,
            },
            LinkCrypterRegistration = linkCrypterRegistration,
            ContainerUrl = "https://crypter.example/container/1",
            State = LinkCrypterContainerState.CreationFailed,
            CreatedAt = DateTime.UtcNow,
        };
        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var repository = new NotificationReadRepository(dbContext);

        // Act
        var result = await repository.GetByIdAsync(notification.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.RelatedEntity.ShouldBe(
            new NotificationRelatedEntityReadModel(
                EntityType: "LinkCrypterContainer",
                DisplayName: "Bearcat.Release.001 / Main upload / Crypter",
                TargetUrl: $"/releases/{uploadConfig.ReleaseId}?tab=uploads&view=configuration&uploadConfigId={uploadConfig.Id}"
            )
        );
    }

    [Test]
    public async Task GetByIdAsync_NotificationLinkedToContainerWithoutUploadConfigLinkCrypter_ReturnsNoRelatedEntity()
    {
        // Arrange
        await using var dbContext = Database.CreateDbContext();
        var notification = CreateNotification("Container failed", DateTime.UtcNow);
        notification.LinkCrypterContainer = new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.ReleaseCollection,
            LinkCrypterRegistration = CreateLinkCrypterRegistration(),
            ContainerUrl = "https://crypter.example/container/1",
            State = LinkCrypterContainerState.CreationFailed,
            CreatedAt = DateTime.UtcNow,
        };
        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var repository = new NotificationReadRepository(dbContext);

        // Act
        var result = await repository.GetByIdAsync(notification.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Message.ShouldBe("Container failed");
        result.RelatedEntity.ShouldBeNull();
    }

    [Test]
    public async Task GetByIdAsync_NotificationLinkedToHosterRegistration_ReturnsHosterRegistrationAsRelatedEntity()
    {
        // Arrange
        await using var dbContext = Database.CreateDbContext();
        var notification = CreateNotification("Captcha required", DateTime.UtcNow);
        notification.HosterRegistration = new HosterRegistration
        {
            Name = "Café hoster",
            SerializedConfig = "{}",
            HosterClassName = "TestHoster",
            IsActive = true,
        };
        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var repository = new NotificationReadRepository(dbContext);

        // Act
        var result = await repository.GetByIdAsync(notification.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.RelatedEntity.ShouldBe(
            new NotificationRelatedEntityReadModel(
                EntityType: "HosterRegistration",
                DisplayName: "Café hoster",
                TargetUrl: "/hoster-registrations"
            )
        );
    }

    [Test]
    public async Task GetByIdAsync_NotificationDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var dbContext = Database.CreateDbContext();
        var notification = CreateNotification("Existing", DateTime.UtcNow);
        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var repository = new NotificationReadRepository(dbContext);

        // Act
        var result = await repository.GetByIdAsync(notification.Id + 1, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
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

    private static Notification CreateNotification(
        string message,
        DateTime createdAt,
        DateTime? resolvedAt = null
    )
    {
        return new Notification
        {
            CreatedAt = createdAt,
            ResolvedAt = resolvedAt,
            NotificationKind = NotificationKind.UploadFailed,
            NotificationSeverity = NotificationSeverity.Error,
            Message = message,
        };
    }

    private static UploadConfig CreateUploadConfig(string releaseName)
    {
        var release = new Release
        {
            Name = releaseName,
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/{releaseName}",
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{releaseName} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };

        return new UploadConfig
        {
            Release = release,
            ArchiveConfig = new ArchiveConfig
            {
                Release = release,
                Name = "Main archive",
                ArchiveFilesBasePath = "/tmp/archive",
                ArchiverName = "zip",
                ArchiveFileSizeMb = 512,
            },
            HosterRegistration = new HosterRegistration
            {
                Name = "Hoster",
                SerializedConfig = "{}",
                HosterClassName = "TestHoster",
                IsActive = true,
            },
            Name = "Main upload",
        };
    }

    private static LinkCrypterRegistration CreateLinkCrypterRegistration()
    {
        return new LinkCrypterRegistration
        {
            Name = "Crypter",
            LinkCrypterClassName = "TestCrypter",
            SerializedConfig = "{}",
            IsActive = true,
        };
    }
}
