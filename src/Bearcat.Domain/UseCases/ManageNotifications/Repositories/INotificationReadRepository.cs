using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageNotifications.Dto;
using Bearcat.Domain.UseCases.ManageNotifications.ReadModels;

namespace Bearcat.Domain.UseCases.ManageNotifications.Repositories;

public interface INotificationReadRepository
{
    Task<int> CountUnresolvedAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationReadModel>> GetLatestUnresolvedAsync(
        int take,
        CancellationToken cancellationToken = default
    );

    Task<NotificationReadModel?> GetByIdAsync(
        int notificationId,
        CancellationToken cancellationToken = default
    );

    Task<PagedResult<NotificationReadModel>> SearchAsync(
        NotificationSearchQuery query,
        CancellationToken cancellationToken = default
    );
}
