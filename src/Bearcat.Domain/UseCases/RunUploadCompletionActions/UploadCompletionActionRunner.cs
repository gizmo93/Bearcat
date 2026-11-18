using Bearcat.Abstractions.Security;
using Bearcat.Abstractions.UploadCompletionAction;
using Bearcat.Abstractions.UploadCompletionAction.Dto;
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
        var results = new List<bool>();

        var registrations = await repository.GetActiveRegistrationsWithReadableSecretsAsync(
            cancellationToken
        );

        foreach (var registration in registrations)
        {
            var action = uploadCompletionActionFactory.GetByClassName(registration.ActionClassName);
            var config = DecryptConfig(action, registration);
            var dueReleases = await repository.GetDueReleasesAsync(registration, cancellationToken);

            var dueReleaseCollections = await repository.GetDueReleaseCollectionsAsync(
                registration,
                cancellationToken
            );

            foreach (var dueRelease in dueReleases)
            {
                results.Add(
                    await ExecuteForReleaseAsync(
                        registration: registration,
                        action: action,
                        config: config,
                        dueRelease: dueRelease,
                        createNotificationOnFirstFailure: true,
                        cancellationToken: cancellationToken
                    )
                );
            }

            foreach (var dueReleaseCollection in dueReleaseCollections)
            {
                results.Add(
                    await ExecuteForReleaseCollectionAsync(
                        registration: registration,
                        action: action,
                        config: config,
                        dueReleaseCollection: dueReleaseCollection,
                        createNotificationOnFirstFailure: true,
                        cancellationToken: cancellationToken
                    )
                );
            }
        }

        if (results.Count > 0)
        {
            logger.LogInformation(
                "Upload completion actions succeeded {SucceededCount} times and failed {FailedCount} times",
                results.Count(succeeded => succeeded),
                results.Count(succeeded => !succeeded)
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

        await ExecuteForReleaseAsync(
            registration: registration,
            action: action,
            config: DecryptConfig(action, registration),
            dueRelease: completelyUploadedRelease,
            createNotificationOnFirstFailure: false,
            cancellationToken: cancellationToken
        );
    }

    public async Task RunForReleaseCollectionAsync(
        int registrationId,
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await repository.GetRegistrationAsync(registrationId, cancellationToken);

        var completelyUploadedReleaseCollection =
            await repository.GetCompletelyUploadedReleaseCollectionAsync(
                releaseCollectionId,
                cancellationToken
            )
            ?? throw new InvalidOperationException(
                $"Release collection {releaseCollectionId} is not completely uploaded"
            );

        var action = uploadCompletionActionFactory.GetByClassName(registration.ActionClassName);

        await ExecuteForReleaseCollectionAsync(
            registration: registration,
            action: action,
            config: DecryptConfig(action, registration),
            dueReleaseCollection: completelyUploadedReleaseCollection,
            createNotificationOnFirstFailure: false,
            cancellationToken: cancellationToken
        );
    }

    private async Task<bool> ExecuteForReleaseAsync(
        UploadCompletionActionRegistration registration,
        IUploadCompletionAction action,
        IUploadCompletionActionConfig config,
        UploadCompletionActionDueRelease dueRelease,
        bool createNotificationOnFirstFailure,
        CancellationToken cancellationToken
    )
    {
        var input = await inputBuilder.BuildAsync(dueRelease, cancellationToken);

        var execution =
            await repository.GetReleaseExecutionAsync(
                releaseId: dueRelease.ReleaseId,
                uploadCompletionActionRegistrationId: registration.Id,
                cancellationToken: cancellationToken
            )
            ?? new UploadCompletionActionExecution
            {
                ReleaseId = dueRelease.ReleaseId,
                UploadCompletionActionRegistrationId = registration.Id,
                HandledUploadedAt = registration.CreatedAt,
            };

        return await ExecuteAndSaveResultAsync(
            registration: registration,
            action: action,
            config: config,
            input: input,
            execution: execution,
            newestUploadedAt: dueRelease.NewestUploadedAt,
            createNotificationOnFirstFailure: createNotificationOnFirstFailure,
            cancellationToken: cancellationToken
        );
    }

    private async Task<bool> ExecuteForReleaseCollectionAsync(
        UploadCompletionActionRegistration registration,
        IUploadCompletionAction action,
        IUploadCompletionActionConfig config,
        UploadCompletionActionDueReleaseCollection dueReleaseCollection,
        bool createNotificationOnFirstFailure,
        CancellationToken cancellationToken
    )
    {
        var input = await inputBuilder.BuildAsync(dueReleaseCollection, cancellationToken);

        var execution =
            await repository.GetReleaseCollectionExecutionAsync(
                releaseCollectionId: dueReleaseCollection.ReleaseCollectionId,
                uploadCompletionActionRegistrationId: registration.Id,
                cancellationToken: cancellationToken
            )
            ?? new UploadCompletionActionExecution
            {
                ReleaseCollectionId = dueReleaseCollection.ReleaseCollectionId,
                UploadCompletionActionRegistrationId = registration.Id,
                HandledUploadedAt = registration.CreatedAt,
            };

        return await ExecuteAndSaveResultAsync(
            registration: registration,
            action: action,
            config: config,
            input: input,
            execution: execution,
            newestUploadedAt: dueReleaseCollection.NewestUploadedAt,
            createNotificationOnFirstFailure: createNotificationOnFirstFailure,
            cancellationToken: cancellationToken
        );
    }

    private async Task<bool> ExecuteAndSaveResultAsync(
        UploadCompletionActionRegistration registration,
        IUploadCompletionAction action,
        IUploadCompletionActionConfig config,
        UploadCompletionActionInput input,
        UploadCompletionActionExecution execution,
        DateTime newestUploadedAt,
        bool createNotificationOnFirstFailure,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var resultText = await action.ExecuteAsync(config, input, cancellationToken);

            await SaveSuccessAsync(
                execution: execution,
                newestUploadedAt: newestUploadedAt,
                resultText: resultText,
                cancellationToken: cancellationToken
            );

            return true;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception,
                "Upload completion action {RegistrationName} failed for {Name}: {Message}",
                registration.Name,
                input.Release.Name,
                exception.Message
            );

            await SaveFailureAsync(
                registration: registration,
                name: input.Release.Name,
                execution: execution,
                errorMessage: exception.Message,
                createNotificationOnFirstFailure: createNotificationOnFirstFailure,
                cancellationToken: cancellationToken
            );

            return false;
        }
    }

    private async Task SaveSuccessAsync(
        UploadCompletionActionExecution execution,
        DateTime newestUploadedAt,
        string resultText,
        CancellationToken cancellationToken
    )
    {
        AddExecutionWhenNew(execution);

        execution.HandledUploadedAt = newestUploadedAt;
        execution.ExecutedAt = timeProvider.GetLocalNow();
        execution.ResultText = Truncate(resultText);
        execution.ErrorMessage = null;

        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task SaveFailureAsync(
        UploadCompletionActionRegistration registration,
        string name,
        UploadCompletionActionExecution execution,
        string errorMessage,
        bool createNotificationOnFirstFailure,
        CancellationToken cancellationToken
    )
    {
        var previousExecutionFailed = execution.ErrorMessage is not null;

        AddExecutionWhenNew(execution);

        execution.ExecutedAt = timeProvider.GetLocalNow();
        execution.ErrorMessage = Truncate(errorMessage);

        await repository.SaveChangesAsync(cancellationToken);

        if (previousExecutionFailed || !createNotificationOnFirstFailure)
        {
            return;
        }

        if (execution.ReleaseId is null)
        {
            await notificationService.CreateAsync(
                kind: NotificationKind.UploadCompletionActionFailed,
                message: Truncate(
                    $"Upload completion action '{registration.Name}' failed for release collection '{name}': {errorMessage}"
                ),
                cancellationToken: cancellationToken
            );

            return;
        }

        await notificationService.CreateAsync(
            kind: NotificationKind.UploadCompletionActionFailed,
            message: Truncate(
                $"Upload completion action '{registration.Name}' failed for release '{name}': {errorMessage}"
            ),
            entity: execution.ReleaseId,
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

    private void AddExecutionWhenNew(UploadCompletionActionExecution execution)
    {
        if (execution.Id == 0)
        {
            repository.AddExecution(execution);
        }
    }

    private static string Truncate(string text)
    {
        return text.Length <= MaximumTextLength ? text : text[..MaximumTextLength];
    }
}
