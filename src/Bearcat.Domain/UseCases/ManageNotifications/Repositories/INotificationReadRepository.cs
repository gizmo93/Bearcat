using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageNotifications.Dto;

namespace Bearcat.Domain.UseCases.ManageNotifications.Repositories;

public interface INotificationReadRepository
{
    Task<int> CountUnresolvedAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationDto>> GetLatestUnresolvedAsync(
        int take,
        CancellationToken cancellationToken = default
    );

    Task<NotificationDto?> GetByIdAsync(
        int notificationId,
        CancellationToken cancellationToken = default
    );

    Task<PagedResult<NotificationDto>> SearchAsync(
        NotificationSearchQuery query,
        CancellationToken cancellationToken = default
    );
}
