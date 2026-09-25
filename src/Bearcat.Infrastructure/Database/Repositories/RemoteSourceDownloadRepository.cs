using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Downloading.Repositories;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.RawFiles.Repositories;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.ReleaseCreation.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class RemoteSourceDownloadRepository(IBearcatWriteDbContext dbWrite)
    : IRemoteSourceDownloadRepository,
        IRemoteDownloadReleaseRepository,
        IRemoteDownloadRawFileCleanupRepository
{
    public async Task<IReadOnlyList<RemoteSourceDownload>> GetInterruptedDownloadsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .RemoteSourceDownloads.Where(download =>
                download.State == RemoteSourceDownloadState.Downloading
            )
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RemoteSourceDownload>> GetPendingDownloadsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .RemoteSourceDownloads.Where(download =>
                download.State == RemoteSourceDownloadState.Pending
                && download.RemoteSourceRegistration != null
                && download.RemoteSourceRegistration.IsActive
            )
            .Include(download => download.RemoteSourceRegistration)
            .OrderBy(download => download.DiscoveredAt)
            .ThenBy(download => download.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<RemoteSourceDownload> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite.RemoteSourceDownloads.FirstAsync(
            download => download.Id == id,
            cancellationToken
        );
    }

    public async Task<bool> TryRefreshAsync(
        RemoteSourceDownload download,
        CancellationToken cancellationToken = default
    )
    {
        var entry = dbWrite.Entry(download);
        await entry.ReloadAsync(cancellationToken);

        if (entry.State == EntityState.Detached)
        {
            return false;
        }

        if (download.RemoteSourceRegistration is { } registration)
        {
            await dbWrite.Entry(registration).ReloadAsync(cancellationToken);
        }

        return true;
    }

    public async Task<IReadOnlyList<RemoteSourceDownload>> GetDownloadedWithoutReleaseAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .RemoteSourceDownloads.Where(download =>
                download.State == RemoteSourceDownloadState.Downloaded && download.ReleaseId == null
            )
            .OrderBy(download => download.CompletedAt)
            .ThenBy(download => download.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<ReleaseTemplate?> GetReleaseTemplateAsync(
        int releaseTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .ReleaseTemplates.AsSplitQuery()
            .Include(template => template.ArchiveConfigTemplates)
                .ThenInclude(archiveTemplate => archiveTemplate.AdditionalArchiveContents)
            .Include(template => template.UploadConfigTemplates)
                .ThenInclude(uploadTemplate => uploadTemplate.HosterRegistration)
            .Include(template => template.UploadConfigTemplates)
                .ThenInclude(uploadTemplate => uploadTemplate.LinkCrypterTemplates)
            .Include(template => template.ImageUploadConfigTemplates)
                .ThenInclude(imageTemplate => imageTemplate.ImageHosterRegistration)
            .Include(template => template.CollectionImageUploadConfigTemplates)
                .ThenInclude(imageTemplate => imageTemplate.ImageHosterRegistration)
            .FirstOrDefaultAsync(template => template.Id == releaseTemplateId, cancellationToken);
    }

    public async Task<IReadOnlyList<RemoteSourceDownload>> GetRawFileCleanupCandidatesAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .RemoteSourceDownloads.AsSplitQuery()
            .Include(download => download.Release!)
                .ThenInclude(release => release.UploadConfigs)
            .Include(download => download.Release!)
                .ThenInclude(release => release.ArchiveConfigs)
                    .ThenInclude(config => config.Archives)
            .Include(download => download.Release!)
                .ThenInclude(release => release.ArchiveConfigs)
                    .ThenInclude(config => config.UploadConfigs)
                        .ThenInclude(uploadConfig => uploadConfig.HosterRegistration)
            .Include(download => download.Release!)
                .ThenInclude(release => release.ArchiveConfigs)
                    .ThenInclude(config => config.UploadConfigs)
                        .ThenInclude(uploadConfig => uploadConfig.Uploads)
                            .ThenInclude(upload => upload.UploadedFiles)
            .Where(download =>
                download.State == RemoteSourceDownloadState.ReleaseCreated
                && !download.KeepRawFiles
                && download.Release != null
                && download.Release.ReleaseType == ReleaseType.Managed
                && download.Release.ReleaseFolderPath == download.LocalFolderPath
            )
            .OrderBy(download => download.Id)
            .ToListAsync(cancellationToken);
    }

    public void Add(Release release)
    {
        dbWrite.Add(release);
    }

    public void DiscardPendingChanges()
    {
        dbWrite.ChangeTracker.DiscardPendingChanges();
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
