namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Cancellation;

public interface IDownloadCancellationRegistry
{
    CancellationToken Register(int archiveId);

    void Unregister(int archiveId);

    bool RequestCancellation(int archiveId);
}
