using Bearcat.Abstractions.Security;
using Bearcat.Abstractions.UploadCompletionAction;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.RunUploadCompletionActions.Models;
using Bearcat.Domain.UseCases.RunUploadCompletionActions.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.RunUploadCompletionActions;

public class UploadCompletionActionRunner(
    IUploadCompletionActionRepository repository,
    IUploadCompletionActionFactory uploadCompletionActionFactory,
    ISecretProtector secretProtector,
    UploadCompletionActionInputBuilder inputBuilder,
    INotificationService notificationService,
    TimeProvider timeProvider,
    ILogger<UploadCompletionActionRunner> logger
)
{
    private const int MaximumTextLength = 2000;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var succeededCount = 0;
        var failedCount = 0;

        var registrations = await repository.GetActiveRegistrationsWithReadableSecretsAsync(
            cancellationToken
        );

        foreach (var registration in registrations)
        {
            var action = uploadCompletionActionFactory.GetByClassName(registration.ActionClassName);
            var config = DecryptConfig(action, registration);
            var dueReleases = await repository.GetDueReleasesAsync(registration, cancellationToken);

            foreach (var dueRelease in dueReleases)
            {
                var succeeded = await ExecuteAndSaveResultAsync(
                    registration: registration,
                    action: action,
                    config: config,
                    dueRelease: dueRelease,
                    createNotificationOnFirstFailure: true,
                    cancellationToken: cancellationToken
                );

                if (succeeded)
                {
                    succeededCount++;
                }
                else
                {
                    failedCount++;
                }
            }
        }

        if (succeededCount > 0 || failedCount > 0)
        {
            logger.LogInformation(
                "Upload completion actions succeeded {SucceededCount} times and failed {FailedCount} times",
                succeededCount,
                failedCount
            );
        }
    }

    public async Task RunForReleaseAsync(
        int registrationId,
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await repository.GetRegistrationAsync(registrationId, cancellationToken);

        var completelyUploadedRelease =
            await repository.GetCompletelyUploadedReleaseAsync(releaseId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Release {releaseId} is not completely uploaded"
            );

        var action = uploadCompletionActionFactory.GetByClassName(registration.ActionClassName);

        await ExecuteAndSaveResultAsync(
            registration: registration,
            action: action,
            config: DecryptConfig(action, registration),
            dueRelease: completelyUploadedRelease,
            createNotificationOnFirstFailure: false,
            cancellationToken: cancellationToken
        );
    }

    private async Task<bool> ExecuteAndSaveResultAsync(
        UploadCompletionActionRegistration registration,
        IUploadCompletionAction action,
        IUploadCompletionActionConfig config,
        UploadCompletionActionDueRelease dueRelease,
        bool createNotificationOnFirstFailure,
        CancellationToken cancellationToken
    )
    {
        var input = await inputBuilder.BuildAsync(dueRelease, cancellationToken);

        var execution = await repository.GetExecutionAsync(
            releaseId: dueRelease.ReleaseId,
            uploadCompletionActionRegistrationId: registration.Id,
            cancellationToken: cancellationToken
        );

        try
        {
            var resultText = await action.ExecuteAsync(config, input, cancellationToken);

            await SaveSuccessAsync(
                registration: registration,
                dueRelease: dueRelease,
                execution: execution,
                resultText: resultText,
                cancellationToken: cancellationToken
            );

            return true;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception,
                "Upload completion action {RegistrationName} failed for release {ReleaseName}: {Message}",
                registration.Name,
                input.Release.Name,
                exception.Message
            );

            await SaveFailureAsync(
                registration: registration,
                dueRelease: dueRelease,
                releaseName: input.Release.Name,
                execution: execution,
                errorMessage: exception.Message,
                createNotificationOnFirstFailure: createNotificationOnFirstFailure,
                cancellationToken: cancellationToken
            );

            return false;
        }
    }

    private async Task SaveSuccessAsync(
        UploadCompletionActionRegistration registration,
        UploadCompletionActionDueRelease dueRelease,
        UploadCompletionActionExecution? execution,
        string resultText,
        CancellationToken cancellationToken
    )
    {
        execution ??= AddExecution(registration, dueRelease.ReleaseId);

        execution.HandledUploadedAt = dueRelease.NewestUploadedAt;
        execution.ExecutedAt = timeProvider.GetLocalNow();
        execution.ResultText = Truncate(resultText);
        execution.ErrorMessage = null;

        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task SaveFailureAsync(
        UploadCompletionActionRegistration registration,
        UploadCompletionActionDueRelease dueRelease,
        string releaseName,
        UploadCompletionActionExecution? execution,
        string errorMessage,
        bool createNotificationOnFirstFailure,
        CancellationToken cancellationToken
    )
    {
        var previousExecutionFailed = execution?.ErrorMessage is not null;

        execution ??= AddExecution(registration, dueRelease.ReleaseId);

        execution.ExecutedAt = timeProvider.GetLocalNow();
        execution.ErrorMessage = Truncate(errorMessage);

        await repository.SaveChangesAsync(cancellationToken);

        if (previousExecutionFailed || !createNotificationOnFirstFailure)
        {
            return;
        }

        await notificationService.CreateAsync(
            kind: NotificationKind.UploadCompletionActionFailed,
            message: Truncate(
                $"Upload completion action '{registration.Name}' failed for release '{releaseName}': {errorMessage}"
            ),
            entity: (int?)dueRelease.ReleaseId,
            selector: notification => notification.ReleaseId,
            cancellationToken: cancellationToken
        );
    }

    private IUploadCompletionActionConfig DecryptConfig(
        IUploadCompletionAction action,
        UploadCompletionActionRegistration registration
    )
    {
        return action.DeserializeConfig(secretProtector.Unprotect(registration.SerializedConfig));
    }

    private UploadCompletionActionExecution AddExecution(
        UploadCompletionActionRegistration registration,
        int releaseId
    )
    {
        var execution = new UploadCompletionActionExecution
        {
            ReleaseId = releaseId,
            UploadCompletionActionRegistrationId = registration.Id,
            HandledUploadedAt = registration.CreatedAt,
        };

        repository.AddExecution(execution);

        return execution;
    }

    private static string Truncate(string text)
    {
        return text.Length <= MaximumTextLength ? text : text[..MaximumTextLength];
    }
}
