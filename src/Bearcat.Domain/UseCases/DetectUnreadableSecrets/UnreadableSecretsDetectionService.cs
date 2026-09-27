using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Entities;
using Bearcat.Domain.UseCases.DetectUnreadableSecrets.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.DetectUnreadableSecrets;

public class UnreadableSecretsDetectionService(
    IUnreadableSecretsRepository repository,
    ISecretProtector secretProtector,
    INotificationService notificationService,
    UnreadableSecretsNotificationService unreadableSecretsNotificationService,
    ILogger<UnreadableSecretsDetectionService> logger
)
{
    public async Task DetectAsync(CancellationToken cancellationToken)
    {
        var newlyUnreadableEntities = new List<IEntityWithEncryptedSecrets>();

        foreach (
            var entity in await repository.GetEntitiesWithEncryptedSecretsAsync(cancellationToken)
        )
        {
            var hasUnreadableSecrets = !secretProtector.CanUnprotect(entity.GetEncryptedSecrets());

            if (hasUnreadableSecrets && !entity.HasUnreadableSecrets)
            {
                newlyUnreadableEntities.Add(entity);
            }

            if (hasUnreadableSecrets && entity is IActivatableEntity activatableEntity)
            {
                activatableEntity.IsActive = false;
            }

            entity.HasUnreadableSecrets = hasUnreadableSecrets;
        }

        var registrationsWithEncryptedSession =
            await repository.GetDistributionSiteRegistrationsWithEncryptedSessionAsync(
                cancellationToken
            );

        foreach (
            var registration in registrationsWithEncryptedSession.Where(registration =>
                !secretProtector.CanUnprotect(registration.EncryptedSession!)
            )
        )
        {
            registration.EncryptedSession = null;

            logger.LogInformation(
                "Discarded the unreadable session of distribution site {DistributionSiteName}",
                registration.Name
            );
        }

        if (newlyUnreadableEntities.Count > 0)
        {
            var affectedRegistrations = FormatAffectedRegistrations(newlyUnreadableEntities);

            logger.LogWarning(
                "Detected {Count} registrations whose stored secrets cannot be decrypted with the current encryption key: {AffectedRegistrations}",
                newlyUnreadableEntities.Count,
                affectedRegistrations
            );

            await notificationService.CreateAsync(
                kind: NotificationKind.UnreadableSecretsDetected,
                message: $"Stored credentials could not be decrypted with the current encryption key and must be entered again: {affectedRegistrations}",
                cancellationToken: cancellationToken
            );
        }

        await repository.SaveChangesAsync(cancellationToken);
        await unreadableSecretsNotificationService.ResolveNotificationWhenNoUnreadableSecretsRemainAsync(
            cancellationToken
        );
    }

    private static string FormatAffectedRegistrations(
        List<IEntityWithEncryptedSecrets> unreadableEntities
    )
    {
        var registrationsGroupedByType = unreadableEntities
            .GroupBy(entity => entity.GetRegistrationTypeName())
            .Select(group =>
            {
                var names = group
                    .Select(entity => entity.GetRegistrationName())
                    .Where(name => name is not null)
                    .ToList();

                return names.Count == 0 ? group.Key : $"{group.Key}: {string.Join(", ", names)}";
            });

        return string.Join("; ", registrationsGroupedByType);
    }
}
