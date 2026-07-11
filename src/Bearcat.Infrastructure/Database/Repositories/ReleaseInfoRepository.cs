using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ReleaseInfoRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite,
    ISecretProtector secretProtector
) : IReleaseInfoRepository
{
    public async Task<
        IReadOnlyList<ActiveNfoDatabaseRegistrationReadModel>
    > GetActiveNfoDatabaseRegistrationsAsync(CancellationToken cancellationToken = default)
    {
        return await dbRead
            .NfoDatabaseRegistrations.Where(registration => registration.IsActive)
            .OrderBy(registration => registration.NfoDatabaseClassName)
            .Select(registration => new ActiveNfoDatabaseRegistrationReadModel(
                registration.NfoDatabaseClassName,
                secretProtector.Unprotect(registration.SerializedConfig)
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Release>> GetReleasesWithoutInfoAsync(
        int count,
        DateTime lastCheckedThreshold,
        HashSet<int> excludedReleaseIds,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .Releases.Include(release => release.ReleaseInfo)
                .ThenInclude(info => info!.ExternalInfos)
            .Include(release => release.ReleaseNfo)
            .Include(release => release.Metadata)
            .Include(release => release.ExternalIdentifiers)
            .Where(release =>
                release.ReleaseInfo == null
                || release.ReleaseNfo == null
                || release.Metadata == null
                || (
                    release.Metadata.MetadataDatabaseClassName != ReleaseMetadata.ManualSource
                    && release.Metadata.CoverUrl == null
                )
            )
            .Where(release =>
                (
                    (release.ReleaseInfo == null || release.ReleaseNfo == null)
                    && (
                        release.ReleaseInfoCheckedAt == null
                        || release.ReleaseInfoCheckedAt < lastCheckedThreshold
                    )
                )
                || (
                    (
                        release.Metadata == null
                        || (
                            release.Metadata.MetadataDatabaseClassName
                                != ReleaseMetadata.ManualSource
                            && release.Metadata.CoverUrl == null
                        )
                    )
                    && (
                        release.MetadataCheckedAt == null
                        || release.MetadataCheckedAt < lastCheckedThreshold
                    )
                )
            )
            .Where(release => !excludedReleaseIds.Contains(release.Id))
            .OrderBy(release => release.CreatedAt)
            .ThenBy(release => release.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasReleaseInfoAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead.ReleaseInfos.AnyAsync(
            info => info.ReleaseId == releaseId,
            cancellationToken
        );
    }

    public async Task<Release> GetReleaseForCoverUpdateAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .Releases.Include(release => release.ReleaseInfo)
            .Include(release => release.Metadata)
            .Include(release => release.ExternalIdentifiers)
            .Include(release => release.ImageUploadConfigs)
                .ThenInclude(config => config.ImageUploads)
            .FirstAsync(release => release.Id == releaseId, cancellationToken);
    }

    public async Task<Release> GetReleaseWithInfoAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .Releases.Include(release => release.ReleaseInfo)
                .ThenInclude(info => info!.ExternalInfos)
            .Include(release => release.Metadata)
            .Include(release => release.ReleaseNfo)
            .Include(release => release.ExternalIdentifiers)
            .Include(release => release.ReleaseCollection)
            .FirstAsync(release => release.Id == releaseId, cancellationToken);
    }

    public void Remove(ReleaseInfo releaseInfo)
    {
        dbWrite.Remove(releaseInfo);
    }

    public void Remove(ReleaseMetadata metadata)
    {
        dbWrite.Remove(metadata);
    }

    public void Remove(ImageUpload imageUpload)
    {
        dbWrite.Remove(imageUpload);
    }

    public void DetachPendingReleaseInfo(Release release)
    {
        var pendingReleaseInfos = dbWrite
            .ChangeTracker.Entries<ReleaseInfo>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .ToList();

        var pendingExternalInfos = dbWrite
            .ChangeTracker.Entries<ReleaseExternalInfo>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .ToList();

        var pendingMetadata = dbWrite
            .ChangeTracker.Entries<ReleaseMetadata>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .ToList();

        foreach (
            var entry in dbWrite
                .ChangeTracker.Entries<ReleaseMetadata>()
                .Where(entry => pendingMetadata.Contains(entry.Entity))
        )
        {
            entry.State = EntityState.Detached;
        }

        foreach (
            var entry in dbWrite
                .ChangeTracker.Entries<ReleaseExternalInfo>()
                .Where(e => pendingExternalInfos.Contains(e.Entity))
        )
        {
            entry.State = EntityState.Detached;
        }

        foreach (
            var entry in dbWrite
                .ChangeTracker.Entries<ReleaseInfo>()
                .Where(e => pendingReleaseInfos.Contains(e.Entity))
        )
        {
            entry.State = EntityState.Detached;
        }

        if (release.ReleaseInfo is not null && pendingReleaseInfos.Contains(release.ReleaseInfo))
        {
            release.ReleaseInfo = null;
        }

        if (release.Metadata is not null && pendingMetadata.Contains(release.Metadata))
        {
            release.Metadata = null;
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
