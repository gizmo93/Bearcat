using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageLinkCrypterContainers.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageLinkCrypterContainers;

public class LinkCrypterContainerService(
    ILinkCrypterContainerCreationWriteRepository repository,
    ILogger<LinkCrypterContainerService> logger,
    ILinkCrypterFactory linkCrypterFactory,
    TimeProvider timeProvider,
    INotificationService notificationService
)
{
    public async Task CreateMissingLinkCrypterContainersAsync(CancellationToken cancellationToken)
    {
        var uploadsToProcess = await repository.GetUploadsWithMissingLinkCrypterContainersAsync(
            cancellationToken
        );

        if (uploadsToProcess.Count == 0)
        {
            logger.LogInformation(
                "No uploads found with missing link crypter containers, finishing"
            );
            return;
        }

        foreach (var upload in uploadsToProcess)
        {
            logger.LogInformation(
                "Processing upload {UploadId} for missing link crypter containers",
                upload.Id
            );
            await ProcessUploadAsync(upload, cancellationToken);
        }

        logger.LogInformation("Finished processing link crypter container creation for uploads");
    }

    private async Task ProcessUploadAsync(Upload upload, CancellationToken cancellationToken)
    {
        var missingConfigs = upload
            .UploadConfig.LinkCrypters.Where(l =>
                l.LinkCrypterRegistration.IsActive
                && !upload
                    .LinkCrypterContainers.Select(c => c.UploadConfigLinkCrypterId)
                    .Contains(l.Id)
            )
            .ToList();

        foreach (var linkCrypterConfig in missingConfigs)
        {
            var previousContainer = upload
                .UploadConfig.Uploads.Where(u =>
                    u.Id < upload.Id
                    && u.LinkCrypterContainers.Any(l =>
                        l.UploadConfigLinkCrypterId == linkCrypterConfig.Id
                    )
                )
                .Select(u =>
                    u.LinkCrypterContainers.First(l =>
                        l.UploadConfigLinkCrypterId == linkCrypterConfig.Id
                    )
                )
                .FirstOrDefault();

            if (previousContainer is not null)
            {
                try
                {
                    await UpdateLinkCrypterContainerAsync(
                        upload: upload,
                        previousContainer: previousContainer,
                        linkCrypterConfig: linkCrypterConfig,
                        cancellationToken: cancellationToken
                    );
                    continue;
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Failed to update link crypter container for upload {UploadId} using link crypter config Id {LinkCrypterId}. Falling back to creating a new container",
                        upload.Id,
                        linkCrypterConfig.Id
                    );
                }
            }

            await CreateLinkCrypterContainerAsync(
                upload: upload,
                linkCrypterConfig: linkCrypterConfig,
                cancellationToken: cancellationToken
            );
        }
    }

    private async Task UpdateLinkCrypterContainerAsync(
        Upload upload,
        LinkCrypterContainer previousContainer,
        UploadConfigLinkCrypter linkCrypterConfig,
        CancellationToken cancellationToken
    )
    {
        var crypter = linkCrypterFactory.Get(
            linkCrypterConfig.LinkCrypterRegistration.LinkCrypterClassName
        );
        var config = crypter.DeserializeConfig(
            linkCrypterConfig.LinkCrypterRegistration.SerializedConfig
        );

        await crypter.UpdateContainerAsync(
            linkCrypterConfig: config,
            containerLink: previousContainer.ContainerUrl,
            externalReference: previousContainer.ExternalReference,
            links: upload.UploadedFiles.Select(uf => uf.HosterFileLink).OrderBy(l => l).ToList(),
            cancellationToken: cancellationToken
        );
    }

    private async Task CreateLinkCrypterContainerAsync(
        Upload upload,
        UploadConfigLinkCrypter linkCrypterConfig,
        CancellationToken cancellationToken
    )
    {
        var crypter = linkCrypterFactory.Get(
            linkCrypterConfig.LinkCrypterRegistration.LinkCrypterClassName
        );
        var config = crypter.DeserializeConfig(
            linkCrypterConfig.LinkCrypterRegistration.SerializedConfig
        );

        var fileUrls = upload.UploadedFiles.Select(f => f.HosterFileLink).OrderBy(l => l).ToList();

        var result = await crypter.CreateContainerAsync(
            linkCrypterConfig: config,
            containerName: Guid.NewGuid().ToString(),
            password: linkCrypterConfig.Password,
            links: fileUrls,
            cancellationToken: cancellationToken
        );

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
                linkCrypterConfig.Id
            );
        }
        else
        {
            logger.LogError(
                "Failed to create link crypter container for upload {UploadId} using link crypter {LinkCrypterId}. Errors: {Errors}",
                upload.Id,
                linkCrypterConfig.Id,
                string.Join("; ", result.ErrorMessages)
            );

            notificationService.CreateError(
                message: $"Failed to create link crypter container for upload {upload.Id} using link crypter {linkCrypterConfig.Id}. Errors: {string.Join("; ", result.ErrorMessages)}",
                entity: container,
                selector: n => n.LinkCrypterContainer
            );
        }

        repository.Add(container);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
