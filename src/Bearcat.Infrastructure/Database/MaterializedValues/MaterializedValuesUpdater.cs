using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.MaterializedValues;

public static class MaterializedValuesUpdater
{
    public static async Task UpdateAsync(
        BearcatDbContext context,
        ReleasesAndUploadsToUpdate releasesAndUploadsToUpdate,
        CancellationToken cancellationToken
    )
    {
        await UpdateOldestUploadedFileCheckedAtAsync(
            context,
            releasesAndUploadsToUpdate.GetUploadIdsAfterSave(),
            cancellationToken
        );

        var releaseIds = releasesAndUploadsToUpdate.GetReleaseIdsAfterSave();
        var uploadConfigIds = releasesAndUploadsToUpdate.GetUploadConfigIdsAfterSave();
        if (uploadConfigIds.Count > 0)
        {
            releaseIds = releaseIds
                .Concat(
                    await context
                        .UploadConfigs.Where(uploadConfig =>
                            uploadConfigIds.Contains(uploadConfig.Id)
                        )
                        .Select(uploadConfig => uploadConfig.ReleaseId)
                        .ToListAsync(cancellationToken)
                )
                .Distinct()
                .Order()
                .ToList();
        }

        await UpdateReleaseOnlineStatesAsync(context, releaseIds, cancellationToken);

        var releaseCollectionIds = releasesAndUploadsToUpdate
            .GetReleaseCollectionIdsAfterSave()
            .Concat(
                await GetReleaseCollectionIdsOfReleasesAsync(context, releaseIds, cancellationToken)
            )
            .Distinct()
            .Order()
            .ToList();

        await UpdateReleaseCollectionOnlineStatesAsync(
            context,
            releaseCollectionIds,
            cancellationToken
        );
    }

    public static async Task<List<int>> GetReleaseCollectionIdsOfReleasesAsync(
        IBearcatWriteDbContext context,
        List<int> releaseIds,
        CancellationToken cancellationToken
    )
    {
        if (releaseIds.Count == 0)
        {
            return [];
        }

        return await context
            .Releases.Where(release =>
                releaseIds.Contains(release.Id) && release.ReleaseCollectionId != null
            )
            .Select(release => release.ReleaseCollectionId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public static async Task UpdateReleaseCollectionOnlineStatesAsync(
        IBearcatWriteDbContext context,
        List<int> releaseCollectionIds,
        CancellationToken cancellationToken
    )
    {
        if (releaseCollectionIds.Count == 0)
        {
            return;
        }

        if (context.Database.IsNpgsql())
        {
            await context.Database.ExecuteSqlAsync(
                $"""SELECT 1 FROM "ReleaseCollections" WHERE "Id" = ANY({releaseCollectionIds}) ORDER BY "Id" FOR UPDATE""",
                cancellationToken
            );
        }

        await context
            .ReleaseCollections.Where(releaseCollection =>
                releaseCollectionIds.Contains(releaseCollection.Id)
            )
            .ExecuteUpdateAsync(
                setters =>
                    setters.SetProperty(
                        releaseCollection => releaseCollection.OnlineState,
                        MaterializedValueExpressions.ReleaseCollectionOnlineState
                    ),
                cancellationToken
            );
    }

    public static async Task UpdateReleaseOnlineStatesAsync(
        IBearcatWriteDbContext context,
        List<int> releaseIds,
        CancellationToken cancellationToken
    )
    {
        if (releaseIds.Count == 0)
        {
            return;
        }

        if (context.Database.IsNpgsql())
        {
            await context.Database.ExecuteSqlAsync(
                $"""SELECT 1 FROM "Releases" WHERE "Id" = ANY({releaseIds}) ORDER BY "Id" FOR UPDATE""",
                cancellationToken
            );
        }

        await context
            .Releases.Where(release => releaseIds.Contains(release.Id))
            .ExecuteUpdateAsync(
                setters =>
                    setters.SetProperty(
                        release => release.OnlineState,
                        MaterializedValueExpressions.ReleaseOnlineState
                    ),
                cancellationToken
            );
    }

    public static async Task UpdateOldestUploadedFileCheckedAtAsync(
        IBearcatWriteDbContext context,
        List<int> uploadIds,
        CancellationToken cancellationToken
    )
    {
        if (uploadIds.Count == 0)
        {
            return;
        }

        if (context.Database.IsNpgsql())
        {
            await context.Database.ExecuteSqlAsync(
                $"""SELECT 1 FROM "Uploads" WHERE "Id" = ANY({uploadIds}) ORDER BY "Id" FOR UPDATE""",
                cancellationToken
            );
        }

        await context
            .Uploads.Where(upload => uploadIds.Contains(upload.Id))
            .ExecuteUpdateAsync(
                setters =>
                    setters.SetProperty(
                        upload => upload.OldestUploadedFileCheckedAt,
                        MaterializedValueExpressions.OldestUploadedFileCheckedAtWithNeverCheckedFilesAsMinValue
                    ),
                cancellationToken
            );
    }
}
