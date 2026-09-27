using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.DetectUnreadableSecrets.Repositories;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.DetectUnreadableSecrets;

public class UnreadableSecretsNotificationService(
    IUnreadableSecretsRepository repository,
    INotificationService notificationService
)
{
    public async Task ResolveNotificationWhenNoUnreadableSecretsRemainAsync(
        CancellationToken cancellationToken
    )
    {
        if (
            !await repository.AnyUnresolvedUnreadableSecretsNotificationAsync(cancellationToken)
            || await repository.AnyUnreadableSecretsAsync(cancellationToken)
        )
        {
            return;
        }

        await notificationService.ResolveAllOfKindAsync(
            NotificationKind.UnreadableSecretsDetected,
            cancellationToken
        );
    }
}
