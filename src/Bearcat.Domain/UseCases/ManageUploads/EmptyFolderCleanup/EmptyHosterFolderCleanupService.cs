using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Proxies;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageUploads.EmptyFolderCleanup;

public class EmptyHosterFolderCleanupService(
    IEmptyHosterFolderCleanupRepository repository,
    IHosterFactory hosterFactory,
    ISecretProtector secretProtector,
    HosterCaptchaVerificationService captchaVerificationService,
    HosterCredentialsRejectionService credentialsRejectionService,
    ILogger<EmptyHosterFolderCleanupService> logger
)
{
    private const int MaximumFoldersToCheckPerHosterRegistrationPerRun = 200;
    private const int MaximumConsecutiveDeleteFailures = 3;

    public async Task DeleteEmptyFoldersAsync(CancellationToken cancellationToken)
    {
        var registrations = await repository.GetHosterRegistrationsWithEmptyFolderDeletionAsync(
            cancellationToken
        );

        foreach (var registration in registrations)
        {
            var hoster = hosterFactory.GetByName(registration.HosterClassName);

            if (hoster is not IHosterWithEmptyFolderCleanup emptyFolderCleanupHoster)
            {
                continue;
            }

            try
            {
                await DeleteEmptyFoldersOfRegistrationAsync(
                    registration: registration,
                    hoster: emptyFolderCleanupHoster,
                    cancellationToken: cancellationToken
                );
            }
            catch (CaptchaVerificationRequiredException ex)
            {
                await captchaVerificationService.MarkRequiredAsync(
                    registration,
                    ex.Message,
                    cancellationToken
                );
            }
            catch (HosterCredentialsRejectedException ex)
            {
                await credentialsRejectionService.DeactivateAndNotifyAsync(
                    registration,
                    ex.Message,
                    cancellationToken
                );
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    ex,
                    "Failed to delete empty folders on hoster registration {HosterRegistration}",
                    registration.Name
                );
            }
        }
    }

    private async Task DeleteEmptyFoldersOfRegistrationAsync(
        HosterRegistration registration,
        IHosterWithEmptyFolderCleanup hoster,
        CancellationToken cancellationToken
    )
    {
        var folderIds = await repository.GetFolderIdsWithoutOnlineFilesAsync(
            registration.Id,
            cancellationToken
        );

        if (folderIds.Count == 0)
        {
            return;
        }

        var shuffledFolderIds = folderIds.ToArray();
        Random.Shared.Shuffle(shuffledFolderIds);

        var folderIdsToCheck = shuffledFolderIds
            .Take(MaximumFoldersToCheckPerHosterRegistrationPerRun)
            .ToList();

        var hosterConfig = hoster.DeserializeHosterConfig(
            secretProtector.Unprotect(registration.SerializedConfig)
        );

        using var proxyScope = RegistrationProxyScope.EnterForHosterUpload(registration);

        var folderContentStates = await hoster.GetFolderContentStatesAsync(
            folderIdsToCheck,
            hosterConfig,
            cancellationToken
        );

        var consecutiveDeleteFailures = 0;

        foreach (var folderContentState in folderContentStates)
        {
            if (folderContentState.State is HosterFolderContentState.NotEmpty)
            {
                continue;
            }

            if (folderContentState.State is HosterFolderContentState.NotFound)
            {
                await repository.ClearFolderIdAsync(
                    registration.Id,
                    folderContentState.FolderId,
                    cancellationToken
                );

                logger.LogInformation(
                    "Cleared folder id {FolderId} of hoster registration {HosterRegistration} because the folder no longer exists on the hoster",
                    folderContentState.FolderId,
                    registration.Name
                );

                continue;
            }

            var isDeleted = await TryDeleteFolderAsync(
                registration: registration,
                hoster: hoster,
                hosterConfig: hosterConfig,
                folderId: folderContentState.FolderId,
                cancellationToken: cancellationToken
            );

            if (isDeleted)
            {
                consecutiveDeleteFailures = 0;
                continue;
            }

            consecutiveDeleteFailures++;

            if (consecutiveDeleteFailures >= MaximumConsecutiveDeleteFailures)
            {
                logger.LogWarning(
                    "Stopped deleting empty folders on hoster registration {HosterRegistration} for this run after {FailureCount} consecutive failed deletions",
                    registration.Name,
                    consecutiveDeleteFailures
                );

                return;
            }
        }
    }

    private async Task<bool> TryDeleteFolderAsync(
        HosterRegistration registration,
        IHosterWithEmptyFolderCleanup hoster,
        IHosterConfig hosterConfig,
        string folderId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await hoster.DeleteFolderAsync(folderId, hosterConfig, cancellationToken);
        }
        catch (Exception ex)
            when (ex
                    is not CaptchaVerificationRequiredException
                        and not HosterCredentialsRejectedException
                && !cancellationToken.IsCancellationRequested
            )
        {
            logger.LogWarning(
                ex,
                "Failed to delete empty folder {FolderId} on hoster registration {HosterRegistration}",
                folderId,
                registration.Name
            );

            return false;
        }

        await repository.ClearFolderIdAsync(registration.Id, folderId, cancellationToken);

        logger.LogInformation(
            "Deleted empty folder {FolderId} on hoster registration {HosterRegistration}",
            folderId,
            registration.Name
        );

        return true;
    }
}
