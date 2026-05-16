using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageNotifications.Dto;
using Bearcat.Domain.UseCases.ManageNotifications.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class NotificationReadRepository(IBearcatReadDbContext dbRead) : INotificationReadRepository
{
    public async Task<int> CountUnresolvedAsync(CancellationToken cancellationToken = default)
    {
        return await dbRead
            .Notifications.Where(n => n.ResolvedAt == null)
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationDto>> GetLatestUnresolvedAsync(
        int take,
        CancellationToken cancellationToken = default
    )
    {
        var notifications = await BaseQuery()
            .Where(n => n.ResolvedAt == null)
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Take(Math.Clamp(take, 1, 25))
            .Select(ToProjection())
            .ToListAsync(cancellationToken);

        return notifications.Select(ToDto).ToList();
    }

    public async Task<NotificationDto?> GetByIdAsync(
        int notificationId,
        CancellationToken cancellationToken = default
    )
    {
        var notification = await BaseQuery()
            .Where(n => n.Id == notificationId)
            .Select(ToProjection())
            .FirstOrDefaultAsync(cancellationToken);

        return notification is null ? null : ToDto(notification);
    }

    public async Task<PagedResult<NotificationDto>> SearchAsync(
        NotificationSearchQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var pageSize = Math.Clamp(query.PageSize, 5, 100);
        var pageIndex = Math.Max(0, query.PageIndex);
        var notificationsQuery = BaseQuery();

        if (!query.IncludeResolved)
        {
            notificationsQuery = notificationsQuery.Where(n => n.ResolvedAt == null);
        }

        var totalCount = await notificationsQuery.CountAsync(cancellationToken);

        var notifications = await notificationsQuery
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .Select(ToProjection())
            .ToListAsync(cancellationToken);

        return new PagedResult<NotificationDto>(
            notifications.Select(ToDto).ToList(),
            totalCount,
            pageIndex,
            pageSize
        );
    }

    private IQueryable<Notification> BaseQuery()
    {
        return dbRead.Notifications.AsNoTracking();
    }

    private static System.Linq.Expressions.Expression<
        Func<Notification, NotificationProjection>
    > ToProjection()
    {
        return n => new NotificationProjection(
            n.Id,
            n.CreatedAt,
            n.ResolvedAt,
            n.NotificationType,
            n.Message,
            n.UploadId,
            n.UploadId == null ? null : n.Upload!.UploadConfigId,
            n.UploadId == null ? null : n.Upload!.UploadConfig.Name,
            n.UploadId == null ? null : n.Upload!.UploadConfig.ReleaseId,
            n.UploadId == null ? null : n.Upload!.UploadConfig.Release.Name,
            n.ArchiveId,
            n.ArchiveId == null ? null : n.Archive!.ArchiveConfigId,
            n.ArchiveId == null ? null : n.Archive!.ArchiveConfig.Name,
            n.ArchiveId == null ? null : n.Archive!.ArchiveConfig.ReleaseId,
            n.ArchiveId == null ? null : n.Archive!.ArchiveConfig.Release.Name,
            n.LinkCrypterContainerId,
            n.LinkCrypterContainerId == null
                ? null
                : n.LinkCrypterContainer!.UploadConfigLinkCrypter.UploadConfigId,
            n.LinkCrypterContainerId == null
                ? null
                : n.LinkCrypterContainer!.UploadConfigLinkCrypter.UploadConfig.Name,
            n.LinkCrypterContainerId == null
                ? null
                : n.LinkCrypterContainer!.UploadConfigLinkCrypter.UploadConfig.ReleaseId,
            n.LinkCrypterContainerId == null
                ? null
                : n.LinkCrypterContainer!.UploadConfigLinkCrypter.UploadConfig.Release.Name,
            n.LinkCrypterContainerId == null
                ? null
                : n.LinkCrypterContainer!.UploadConfigLinkCrypter.LinkCrypterRegistration.Name
        );
    }

    private static NotificationDto ToDto(NotificationProjection notification)
    {
        return new NotificationDto(
            notification.Id,
            notification.CreatedAt,
            notification.ResolvedAt,
            notification.NotificationType,
            notification.Message,
            CreateRelatedEntity(notification)
        );
    }

    private static NotificationRelatedEntityDto? CreateRelatedEntity(
        NotificationProjection notification
    )
    {
        if (
            notification.UploadId is not null
            && notification.UploadConfigId is not null
            && notification.UploadReleaseId is not null
        )
        {
            return new NotificationRelatedEntityDto(
                "Upload",
                JoinDisplayName(notification.UploadReleaseName, notification.UploadConfigName),
                $"/releases/{notification.UploadReleaseId}?tab=uploads&uploadConfigId={notification.UploadConfigId}"
            );
        }

        if (
            notification.ArchiveId is not null
            && notification.ArchiveConfigId is not null
            && notification.ArchiveReleaseId is not null
        )
        {
            return new NotificationRelatedEntityDto(
                "Archive",
                JoinDisplayName(notification.ArchiveReleaseName, notification.ArchiveConfigName),
                $"/releases/{notification.ArchiveReleaseId}?tab=archives&archiveConfigId={notification.ArchiveConfigId}"
            );
        }

        if (
            notification.LinkCrypterContainerId is not null
            && notification.LinkUploadConfigId is not null
            && notification.LinkReleaseId is not null
        )
        {
            return new NotificationRelatedEntityDto(
                "LinkCrypterContainer",
                JoinDisplayName(
                    notification.LinkReleaseName,
                    notification.LinkUploadConfigName,
                    notification.LinkCrypterName
                ),
                $"/releases/{notification.LinkReleaseId}?tab=upload-configs&uploadConfigId={notification.LinkUploadConfigId}"
            );
        }

        return null;
    }

    private static string JoinDisplayName(params string?[] parts)
    {
        var displayName = string.Join(" / ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        return string.IsNullOrWhiteSpace(displayName) ? "Unlinked" : displayName;
    }

    private sealed record NotificationProjection(
        int Id,
        DateTime CreatedAt,
        DateTime? ResolvedAt,
        NotificationType NotificationType,
        string Message,
        int? UploadId,
        int? UploadConfigId,
        string? UploadConfigName,
        int? UploadReleaseId,
        string? UploadReleaseName,
        int? ArchiveId,
        int? ArchiveConfigId,
        string? ArchiveConfigName,
        int? ArchiveReleaseId,
        string? ArchiveReleaseName,
        int? LinkCrypterContainerId,
        int? LinkUploadConfigId,
        string? LinkUploadConfigName,
        int? LinkReleaseId,
        string? LinkReleaseName,
        string? LinkCrypterName
    );
}
