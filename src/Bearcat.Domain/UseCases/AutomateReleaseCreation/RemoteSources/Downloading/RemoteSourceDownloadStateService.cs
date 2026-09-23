using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Downloading.Repositories;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Downloading;

public class RemoteSourceDownloadStateService(
    IRemoteSourceDownloadRepository repository,
    ITransferCancellationRegistry cancellationRegistry,
    RemoteDownloadFolderService folderService
)
{
    public async Task CancelDownloadAsync(int id, CancellationToken cancellationToken = default)
    {
        var download = await repository.GetByIdAsync(id, cancellationToken);

        if (
            cancellationRegistry.RequestCancellation(
                RemoteSourceDownloadService.CreateTransferKey(download.Id)
            )
        )
        {
            return;
        }

        switch (download.State)
        {
            case RemoteSourceDownloadState.Observing:
            case RemoteSourceDownloadState.Pending:
                break;
            case RemoteSourceDownloadState.Downloading:
                folderService.DeleteDownloadedFiles(download);
                break;
            default:
                throw InvalidTransition(download.State, RemoteSourceDownloadState.Canceled);
        }

        download.State = RemoteSourceDownloadState.Canceled;
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task RestartDownloadAsync(int id, CancellationToken cancellationToken = default)
    {
        var download = await repository.GetByIdAsync(id, cancellationToken);

        if (
            download.State
            is not (RemoteSourceDownloadState.Failed or RemoteSourceDownloadState.Canceled)
        )
        {
            throw InvalidTransition(download.State, RemoteSourceDownloadState.Pending);
        }

        if (download.StartedAt is not null)
        {
            folderService.DeleteDownloadedFiles(download);
        }

        download.State = RemoteSourceDownloadState.Pending;
        download.ErrorMessage = null;
        download.StartedAt = null;
        download.CompletedAt = null;
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task IgnoreDownloadAsync(int id, CancellationToken cancellationToken = default)
    {
        var download = await repository.GetByIdAsync(id, cancellationToken);

        if (
            download.State
            is not (
                RemoteSourceDownloadState.Observing
                or RemoteSourceDownloadState.Pending
                or RemoteSourceDownloadState.Failed
                or RemoteSourceDownloadState.Canceled
            )
        )
        {
            throw InvalidTransition(download.State, RemoteSourceDownloadState.Ignored);
        }

        download.State = RemoteSourceDownloadState.Ignored;
        await repository.SaveChangesAsync(cancellationToken);
    }

    private static InvalidOperationException InvalidTransition(
        RemoteSourceDownloadState currentState,
        RemoteSourceDownloadState targetState
    )
    {
        return new InvalidOperationException(
            $"A remote download in state {currentState} cannot be changed to {targetState}."
        );
    }
}
