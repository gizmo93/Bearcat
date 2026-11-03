using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageLinkCrypterContainers.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageLinkCrypterContainers.MultiUploadContainers;

public class MultiUploadLinkCrypterContainerService(
    ILinkCrypterContainerCreationWriteRepository repository,
    LinkCrypterCallService linkCrypterCallService,
    TimeProvider timeProvider,
    INotificationService notificationService,
    ILogger<MultiUploadLinkCrypterContainerService> logger
)
{
    public const int UpdateFailedRetryDelayInMinutes = 15;

    public async Task AddContainerAsync(
        MultiUploadLinkCrypterContainerDefinition definition,
        LinkCrypterContainer container,
        CancellationToken cancellationToken
    )
    {
        await CreateContainerAtLinkCrypterAndApplyResultAsync(
            definition,
            container,
            cancellationToken
        );

        repository.Add(container);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task AddInvalidContainerAsync(
        MultiUploadLinkCrypterContainerDefinition definition,
        LinkCrypterContainer container,
        string errorMessage,
        CancellationToken cancellationToken
    )
    {
        container.CopySettingsFrom(definition.LinkCrypterConfig);
        container.ApplyCreateContainerResult(
            new CreateContainerResult(
                IsSuccess: false,
                ContainerLink: null,
                ExternalReference: null,
                ErrorMessages: [errorMessage]
            ),
            timeProvider.GetLocalNow()
        );
        container.ReplaceSourceUploads(definition.Uploads);

        repository.Add(container);
        await repository.SaveChangesAsync(cancellationToken);

        NotifyContainerInvalid(definition, container, errorMessage);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateContainerAsync(
        MultiUploadLinkCrypterContainerDefinition definition,
        LinkCrypterContainer container,
        bool ignoreUpdateRetryDelay,
        CancellationToken cancellationToken
    )
    {
        if (container.State == LinkCrypterContainerState.CreationFailed)
        {
            await CreateContainerAtLinkCrypterAndApplyResultAsync(
                definition,
                container,
                cancellationToken
            );
            await repository.SaveChangesAsync(cancellationToken);
            return;
        }

        if (!ignoreUpdateRetryDelay && IsWithinUpdateRetryDelay(container))
        {
            return;
        }

        var result = await linkCrypterCallService.SendUpdateContainerRequestAsync(
            linkCrypterConfig: definition.LinkCrypterConfig,
            container: container,
            linksPerMirrorNumber: definition.LinksPerMirrorNumber,
            cancellationToken: cancellationToken
        );

        switch (result.Outcome)
        {
            case UpdateContainerOutcome.Success:
                container.CopySettingsFrom(definition.LinkCrypterConfig);
                container.MarkAsUpdated(result.StatusImageId);
                container.ReplaceSourceUploads(definition.Uploads);
                break;
            case UpdateContainerOutcome.ContainerNotFound:
            {
                var createResult = await CreateNewContainerAndReplaceUrlAsync(
                    definition,
                    container,
                    cancellationToken
                );

                if (!createResult.IsSuccess)
                {
                    container.CopySettingsFrom(definition.LinkCrypterConfig);
                    container.ApplyCreateContainerResult(createResult, timeProvider.GetLocalNow());
                    NotifyContainerCreationFailed(definition, container, createResult);
                }

                break;
            }
            case UpdateContainerOutcome.Failed:
                MarkContainerAsUpdateFailed(definition, container, result.ErrorMessage);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown update container outcome {result.Outcome}."
                );
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task RecreateContainerAsync(
        MultiUploadLinkCrypterContainerDefinition definition,
        LinkCrypterContainer container,
        CancellationToken cancellationToken
    )
    {
        var result = await CreateNewContainerAndReplaceUrlAsync(
            definition,
            container,
            cancellationToken
        );

        if (!result.IsSuccess)
        {
            var errorMessage = string.Join("; ", result.ErrorMessages);
            container.MarkAsUpdateFailed(errorMessage, timeProvider.GetLocalNow());
            logger.LogError(
                "Failed to recreate {ContainerDescription} {ContainerId} for {OwnerDescription}, keeping the previous container. Errors: {Errors}",
                definition.ContainerDescriptionInMessages,
                container.Id,
                definition.OwnerDescriptionInMessages,
                errorMessage
            );
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkContainerAsInvalidAsync(
        MultiUploadLinkCrypterContainerDefinition definition,
        LinkCrypterContainer container,
        string errorMessage,
        CancellationToken cancellationToken
    )
    {
        var previousState = container.State;

        container.Errors = [errorMessage];
        container.State = container.HasContainerUrl()
            ? LinkCrypterContainerState.UpdateFailed
            : LinkCrypterContainerState.CreationFailed;

        if (container.State != previousState)
        {
            NotifyContainerInvalid(definition, container, errorMessage);
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveContainersAsync(
        IReadOnlyList<LinkCrypterContainer> containers,
        CancellationToken cancellationToken
    )
    {
        if (containers.Count == 0)
        {
            return;
        }

        foreach (var container in containers)
        {
            repository.Remove(container);
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    private bool IsWithinUpdateRetryDelay(LinkCrypterContainer container)
    {
        return container.State == LinkCrypterContainerState.UpdateFailed
            && container.UpdateFailedAt
                > timeProvider.GetLocalNow().AddMinutes(-UpdateFailedRetryDelayInMinutes);
    }

    private async Task CreateContainerAtLinkCrypterAndApplyResultAsync(
        MultiUploadLinkCrypterContainerDefinition definition,
        LinkCrypterContainer container,
        CancellationToken cancellationToken
    )
    {
        var result = await linkCrypterCallService.SendCreateContainerRequestAsync(
            linkCrypterConfig: definition.LinkCrypterConfig,
            containerName: definition.ContainerName,
            linksPerMirrorNumber: definition.LinksPerMirrorNumber,
            cancellationToken: cancellationToken
        );

        container.CopySettingsFrom(definition.LinkCrypterConfig);
        container.ApplyCreateContainerResult(result, timeProvider.GetLocalNow());
        container.ReplaceSourceUploads(definition.Uploads);

        if (!result.IsSuccess)
        {
            NotifyContainerCreationFailed(definition, container, result);
        }
    }

    private async Task<CreateContainerResult> CreateNewContainerAndReplaceUrlAsync(
        MultiUploadLinkCrypterContainerDefinition definition,
        LinkCrypterContainer container,
        CancellationToken cancellationToken
    )
    {
        var previousContainerUrl = container.ContainerUrl;
        var result = await linkCrypterCallService.SendCreateContainerRequestAsync(
            linkCrypterConfig: definition.LinkCrypterConfig,
            containerName: definition.ContainerName,
            linksPerMirrorNumber: definition.LinksPerMirrorNumber,
            cancellationToken: cancellationToken
        );

        if (!result.IsSuccess)
        {
            return result;
        }

        container.CopySettingsFrom(definition.LinkCrypterConfig);
        container.ApplyCreateContainerResult(result, timeProvider.GetLocalNow());
        container.ReplaceSourceUploads(definition.Uploads);
        definition.ResetUploadsPostedAt();

        logger.LogWarning(
            "{ContainerDescription} {ContainerId} for {OwnerDescription} was recreated. Previous URL: {PreviousContainerUrl}, new URL: {ContainerUrl}",
            definition.ContainerDescriptionInMessages,
            container.Id,
            definition.OwnerDescriptionInMessages,
            previousContainerUrl,
            container.ContainerUrl
        );

        notificationService.Create(
            kind: NotificationKind.LinkCrypterContainerRecreated,
            message: $"{definition.ContainerDescriptionInMessages} {container.Id} for {definition.OwnerDescriptionInMessages} with crypter {definition.LinkCrypterConfig.LinkCrypterRegistration.Name} was recreated. Previous URL: {previousContainerUrl}, new URL: {container.ContainerUrl}. Existing posts must be updated.",
            entity: container,
            selector: n => n.LinkCrypterContainer
        );

        return result;
    }

    private void MarkContainerAsUpdateFailed(
        MultiUploadLinkCrypterContainerDefinition definition,
        LinkCrypterContainer container,
        string? errorMessage
    )
    {
        var wasAlreadyUpdateFailed = container.State == LinkCrypterContainerState.UpdateFailed;

        container.MarkAsUpdateFailed(errorMessage, timeProvider.GetLocalNow());

        logger.LogError(
            "Failed to update {ContainerDescription} {ContainerId} using link crypter config Id {LinkCrypterId}. Errors: {Errors}",
            definition.ContainerDescriptionInMessages,
            container.Id,
            definition.LinkCrypterConfig.Id,
            errorMessage
        );

        if (wasAlreadyUpdateFailed)
        {
            return;
        }

        notificationService.Create(
            kind: NotificationKind.LinkCrypterContainerUpdateFailed,
            message: $"Failed to update {GetContainerDescriptionInsideSentence(definition)} {container.Id} using link crypter config Id {definition.LinkCrypterConfig.Id} with crypter {definition.LinkCrypterConfig.LinkCrypterRegistration.Name}. The update is retried automatically. Errors: {errorMessage}",
            entity: container,
            selector: n => n.LinkCrypterContainer
        );
    }

    private void NotifyContainerCreationFailed(
        MultiUploadLinkCrypterContainerDefinition definition,
        LinkCrypterContainer container,
        CreateContainerResult result
    )
    {
        notificationService.Create(
            kind: NotificationKind.LinkCrypterContainerCreationFailed,
            message: $"Failed to create {GetContainerDescriptionInsideSentence(definition)} for {definition.OwnerDescriptionInMessages} using link crypter {definition.LinkCrypterConfig.Id}. Errors: {string.Join("; ", result.ErrorMessages)}",
            entity: container,
            selector: n => n.LinkCrypterContainer
        );
    }

    private void NotifyContainerInvalid(
        MultiUploadLinkCrypterContainerDefinition definition,
        LinkCrypterContainer container,
        string errorMessage
    )
    {
        notificationService.Create(
            kind: definition.InvalidNotificationKind,
            message: $"{definition.ContainerDescriptionInMessages} {container.Id} is invalid: {errorMessage}",
            entity: container,
            selector: n => n.LinkCrypterContainer
        );
    }

    private static string GetContainerDescriptionInsideSentence(
        MultiUploadLinkCrypterContainerDefinition definition
    )
    {
        return definition.ContainerDescriptionInMessages.ToLowerInvariant();
    }
}
