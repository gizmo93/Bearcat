using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Importing;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Reading;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers;

public class LinkCrypterImportProcessingService(
    ILinkCrypterImportRepository repository,
    LinkCrypterImportContainerReadingService containerReadingService,
    LinkCrypterImportPreviewService previewService,
    LinkCrypterImportContainerImportService containerImportService,
    INotificationService notificationService,
    TimeProvider timeProvider,
    ILogger<LinkCrypterImportProcessingService> logger
)
{
    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var linkCrypterImportIds = await repository.GetIdsOfImportsWithBackgroundWorkAsync(
            cancellationToken
        );

        foreach (var linkCrypterImportId in linkCrypterImportIds)
        {
            repository.ClearChangeTracker();

            try
            {
                await ProcessImportAsync(linkCrypterImportId, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(
                    exception,
                    "Link crypter import {LinkCrypterImportId} failed",
                    linkCrypterImportId
                );

                await MarkAsFailedAsync(linkCrypterImportId, exception, cancellationToken);
            }
        }
    }

    private async Task ProcessImportAsync(
        int linkCrypterImportId,
        CancellationToken cancellationToken
    )
    {
        var linkCrypterImport = await repository.GetWithContainersAsync(
            linkCrypterImportId,
            cancellationToken
        );

        switch (linkCrypterImport.State)
        {
            case LinkCrypterImportState.ReadingContainers:
                await containerReadingService.ReadContainersAsync(
                    linkCrypterImport,
                    cancellationToken
                );
                break;
            case LinkCrypterImportState.CheckingFiles:
                await previewService.CheckFilesAndCreatePreviewAsync(
                    linkCrypterImport,
                    cancellationToken
                );
                break;
            case LinkCrypterImportState.Importing:
                await containerImportService.ImportSelectedContainersAsync(
                    linkCrypterImportId,
                    cancellationToken
                );
                break;
        }
    }

    private async Task MarkAsFailedAsync(
        int linkCrypterImportId,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        repository.ClearChangeTracker();

        var linkCrypterImport = await repository.GetByIdAsync(
            linkCrypterImportId,
            cancellationToken
        );

        linkCrypterImport.MarkAsFailed([exception.Message], timeProvider.GetLocalNow());

        notificationService.Create(
            kind: NotificationKind.LinkCrypterImportFailed,
            message: $"Link crypter import {linkCrypterImportId} failed: {exception.Message}"
        );
        await repository.SaveChangesAsync(cancellationToken);
    }
}
