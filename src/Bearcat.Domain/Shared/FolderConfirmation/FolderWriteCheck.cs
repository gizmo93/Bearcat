using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.Shared.FolderConfirmation;

public class FolderWriteCheck(
    FolderConfirmationCheck folderConfirmationCheck,
    IConfirmedFolderRepository repository,
    INotificationService notificationService,
    ILogger<FolderWriteCheck> logger
)
{
    public async Task<bool> IsWriteAllowedOtherwiseNotifyAsync(
        string? folderPath,
        CancellationToken cancellationToken
    )
    {
        return await IsWriteAllowedOtherwiseNotifyAsync([folderPath], cancellationToken);
    }

    public async Task<bool> IsWriteAllowedOtherwiseNotifyAsync(
        IReadOnlyList<string?> folderPaths,
        CancellationToken cancellationToken
    )
    {
        var isWriteAllowed = true;

        foreach (
            var folderPath in folderPaths
                .OfType<string>()
                .Where(folderPath => !string.IsNullOrWhiteSpace(folderPath))
                .Distinct(StringComparer.Ordinal)
        )
        {
            var result = await folderConfirmationCheck.GetFolderConfirmationAsync(
                folderPath,
                cancellationToken
            );

            if (result.IsWriteAllowed)
            {
                continue;
            }

            isWriteAllowed = false;
            await NotifyOnceAsync(folderPath, result, cancellationToken);
        }

        return isWriteAllowed;
    }

    public async Task NotifyOnceAsync(
        string folderPath,
        FolderConfirmationResult result,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Skipping the work in {FolderPath} because its folder {RootPath} is in state {FolderConfirmationState}",
            folderPath,
            result.RootPath,
            result.State
        );

        var message = FolderNotConfirmedNotificationMessage.Get(result);

        if (
            await repository.AnyUnresolvedFolderNotConfirmedNotificationAsync(
                message,
                cancellationToken
            )
        )
        {
            return;
        }

        await notificationService.CreateAsync(
            kind: NotificationKind.FolderNotConfirmed,
            message: message,
            cancellationToken: cancellationToken
        );
    }
}
