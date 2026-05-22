using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ReleaseInfoRepository(IBearcatReadDbContext dbRead, IBearcatWriteDbContext dbWrite)
    : IReleaseInfoRepository
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
                registration.SerializedConfig
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Release>> GetReleasesWithoutInfoAsync(
        int count,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .Releases.Include(release => release.ReleaseInfos)
            .Where(release => !release.ReleaseInfos.Any())
            .OrderBy(release => release.CreatedAt)
            .ThenBy(release => release.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasReleaseInfoAsync(
        int releaseId,
        string nfoDatabaseClassName,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead.ReleaseInfos.AnyAsync(
            info =>
                info.ReleaseId == releaseId && info.NfoDatabaseClassName == nfoDatabaseClassName,
            cancellationToken
        );
    }

    public void DetachPendingReleaseInfos(Release release)
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

        foreach (var entry in dbWrite.ChangeTracker.Entries<ReleaseExternalInfo>())
        {
            if (pendingExternalInfos.Contains(entry.Entity))
            {
                entry.State = EntityState.Detached;
            }
        }

        foreach (var entry in dbWrite.ChangeTracker.Entries<ReleaseInfo>())
        {
            if (pendingReleaseInfos.Contains(entry.Entity))
            {
                entry.State = EntityState.Detached;
            }
        }

        release.ReleaseInfos.RemoveAll(info => pendingReleaseInfos.Contains(info));
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
