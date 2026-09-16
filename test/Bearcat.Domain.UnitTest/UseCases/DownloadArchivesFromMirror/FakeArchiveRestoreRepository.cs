using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Repositories;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UnitTest.UseCases.DownloadArchivesFromMirror;

public class FakeArchiveRestoreRepository : IArchiveRestoreRepository
{
    public List<Archive> Archives { get; } = [];

    public List<Upload> Uploads { get; } = [];

    public Dictionary<int, string> SerializedConfigs { get; } = new();

    public int SaveChangesCallCount { get; private set; }

    public Task<IReadOnlyList<Archive>> GetInterruptedRestoresAsync(
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult<IReadOnlyList<Archive>>(
            Archives.Where(a => a.ArchiveState == ArchiveState.Restoring).ToList()
        );
    }

    public Task<IReadOnlyList<Upload>> GetUploadsWaitingForRestoreAsync(
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult<IReadOnlyList<Upload>>(
            Uploads
                .Where(u =>
                    u.ArchiveId is null
                    && u.UploadState == UploadState.WaitingForArchive
                    && u.UploadConfig.Release.ReleaseType == ReleaseType.Remote
                )
                .ToList()
        );
    }

    public Task<Archive?> GetLatestArchiveAsync(
        int archiveConfigId,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(
            Archives.Where(a => a.ArchiveConfigId == archiveConfigId).MaxBy(a => a.Id)
        );
    }

    public Task<IReadOnlyList<Upload>> GetUploadsOfArchiveAsync(
        int archiveId,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult<IReadOnlyList<Upload>>(
            Uploads.Where(u => u.ArchiveId == archiveId).ToList()
        );
    }

    public Task<string> GetSerializedConfigAsync(
        int hosterRegistrationId,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(SerializedConfigs[hosterRegistrationId]);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.FromResult(0);
    }
}
