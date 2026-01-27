using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.CreateLinkCrypterContainers.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.CreateLinkCrypterContainers;

public class LinkCrypterContainerCreationService(
    ILinkCrypterContainerCreationWriteRepository repository,
    ILogger<LinkCrypterContainerCreationService> logger,
    ILinkCrypterFactory linkCrypterFactory,
    TimeProvider timeProvider,
    INotificationService notificationService)
{
    public async Task CreateMissingLinkCrypterContainersAsync(CancellationToken cancellationToken)
    {
        var uploadsToProcess =
            await repository.GetUploadsWithMissingLinkCrypterContainersAsync(cancellationToken);

        if (uploadsToProcess.Count == 0)
        {
            logger.LogInformation("No uploads found with missing link crypter containers, finishing");
            return;
        }

        foreach (var upload in uploadsToProcess)
        {
            logger.LogInformation("Processing upload {UploadId} for missing link crypter containers",
                upload.Id);
            await ProcessUploadAsync(upload, cancellationToken);
        }

        logger.LogInformation("Finished processing link crypter container creation for uploads");
    }

    private async Task ProcessUploadAsync(Upload upload, CancellationToken cancellationToken)
    {
        var missingConfigs = upload.UploadConfig.LinkCrypters
            .Where(l => l.LinkCrypterRegistration.IsActive
                        && !upload.LinkCrypterContainers
                            .Select(c => c.UploadConfigLinkCrypterId)
                            .Contains(l.Id))
            .ToList();

        foreach (var linkCrypterConfig in missingConfigs)
        {
            await CreateLinkCrypterContainerAsync(
                upload: upload,
                linkCrypterConfig: linkCrypterConfig,
                cancellationToken: cancellationToken);
        }
    }

    private async Task CreateLinkCrypterContainerAsync(
        Upload upload,
        UploadConfigLinkCrypter linkCrypterConfig,
        CancellationToken cancellationToken)
    {
        var crypter = linkCrypterFactory.Get(linkCrypterConfig.LinkCrypterRegistration.LinkCrypterClassName);
        var config = crypter.DeserializeConfig(linkCrypterConfig.LinkCrypterRegistration.SerializedConfig);

        var fileUrls = upload.UploadedFiles
            .Select(f => f.HosterFileLink)
            .ToList();

        var result = await crypter.CreateContainerAsync(
            linkCrypterConfig: config,
            containerName: Guid.NewGuid().ToString(),
            password: linkCrypterConfig.Password,
            links: fileUrls,
            cancellationToken: cancellationToken);
        
        var container = new LinkCrypterContainer
        {
            UploadConfigLinkCrypter = linkCrypterConfig,
            Upload = upload,
            ExternalReference = result.ExternalReference,
            ContainerUrl = result.ContainerLink ?? string.Empty,
            Password = linkCrypterConfig.Password,
            Errors = result.ErrorMessages.ToList(),
            CreatedAt = timeProvider.GetLocalNow(),
            State = result.IsSuccess
                ? LinkCrypterContainerState.Created
                : LinkCrypterContainerState.CreationFailed,
        };

        if (result.IsSuccess)
        {
            logger.LogInformation(
                "Successfully created link crypter container for upload {UploadId} using link crypter {LinkCrypterId}",
                upload.Id,
                linkCrypterConfig.Id);
        }
        else
        {
            logger.LogError(
                "Failed to create link crypter container for upload {UploadId} using link crypter {LinkCrypterId}. Errors: {Errors}",
                upload.Id,
                linkCrypterConfig.Id,
                string.Join("; ", result.ErrorMessages));
            
            notificationService.CreateError(
                message: $"Failed to create link crypter container for upload {upload.Id} using link crypter {linkCrypterConfig.Id}. Errors: {string.Join("; ", result.ErrorMessages)}",
                entity: new LinkCrypterContainerNotification { LinkCrypterContainer = container },
                selector: n => n.LinkCrypterContainerNotification);
        }
        
        repository.Add(container);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
