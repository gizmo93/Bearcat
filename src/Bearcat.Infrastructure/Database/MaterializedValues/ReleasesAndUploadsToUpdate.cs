using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Bearcat.Infrastructure.Database.MaterializedValues;

public sealed class ReleasesAndUploadsToUpdate
{
    private readonly HashSet<int> releaseIds = [];
    private readonly HashSet<int> uploadConfigIds = [];
    private readonly HashSet<int> uploadIds = [];
    private readonly List<PropertyEntry> releaseIdPropertiesToReadAfterSave = [];
    private readonly List<PropertyEntry> uploadConfigIdPropertiesToReadAfterSave = [];
    private readonly List<PropertyEntry> uploadIdPropertiesToReadAfterSave = [];
    private readonly List<int> deletedHosterRegistrationIds = [];
    private readonly List<int> deletedArchiveConfigIds = [];
    private readonly List<int> deletedArchiveIds = [];
    private readonly List<int> deletedArchiveFileIds = [];

    public bool IsEmpty =>
        releaseIds.Count == 0
        && uploadConfigIds.Count == 0
        && uploadIds.Count == 0
        && releaseIdPropertiesToReadAfterSave.Count == 0
        && uploadConfigIdPropertiesToReadAfterSave.Count == 0
        && uploadIdPropertiesToReadAfterSave.Count == 0
        && deletedHosterRegistrationIds.Count == 0
        && deletedArchiveConfigIds.Count == 0
        && deletedArchiveIds.Count == 0
        && deletedArchiveFileIds.Count == 0;

    public static ReleasesAndUploadsToUpdate CollectFromPendingChanges(ChangeTracker changeTracker)
    {
        var releasesAndUploadsToUpdate = new ReleasesAndUploadsToUpdate();

        foreach (var entry in changeTracker.Entries())
        {
            switch (entry.Entity)
            {
                case UploadConfig:
                    AddChangedForeignKey(
                        entry,
                        nameof(UploadConfig.ReleaseId),
                        [],
                        releasesAndUploadsToUpdate.releaseIds,
                        releasesAndUploadsToUpdate.releaseIdPropertiesToReadAfterSave
                    );
                    break;
                case Upload:
                    AddChangedForeignKey(
                        entry,
                        nameof(Upload.UploadConfigId),
                        [nameof(Upload.OnlineState)],
                        releasesAndUploadsToUpdate.uploadConfigIds,
                        releasesAndUploadsToUpdate.uploadConfigIdPropertiesToReadAfterSave
                    );
                    break;
                case UploadedFile:
                    AddChangedForeignKey(
                        entry,
                        nameof(UploadedFile.UploadId),
                        [nameof(UploadedFile.CheckedAt)],
                        releasesAndUploadsToUpdate.uploadIds,
                        releasesAndUploadsToUpdate.uploadIdPropertiesToReadAfterSave
                    );
                    break;
                case HosterRegistration hosterRegistration when entry.State == EntityState.Deleted:
                    releasesAndUploadsToUpdate.deletedHosterRegistrationIds.Add(
                        hosterRegistration.Id
                    );
                    break;
                case ArchiveConfig archiveConfig when entry.State == EntityState.Deleted:
                    releasesAndUploadsToUpdate.deletedArchiveConfigIds.Add(archiveConfig.Id);
                    break;
                case Archive archive when entry.State == EntityState.Deleted:
                    releasesAndUploadsToUpdate.deletedArchiveIds.Add(archive.Id);
                    break;
                case ArchiveFile archiveFile when entry.State == EntityState.Deleted:
                    releasesAndUploadsToUpdate.deletedArchiveFileIds.Add(archiveFile.Id);
                    break;
            }
        }

        return releasesAndUploadsToUpdate;
    }

    public async Task AddRowsChangedByDatabaseCascadeDeletesAsync(
        BearcatDbContext context,
        CancellationToken cancellationToken
    )
    {
        if (deletedHosterRegistrationIds.Count > 0)
        {
            releaseIds.UnionWith(
                await context
                    .UploadConfigs.Where(uploadConfig =>
                        uploadConfig.HosterRegistrationId != null
                        && deletedHosterRegistrationIds.Contains(
                            uploadConfig.HosterRegistrationId.Value
                        )
                    )
                    .Select(uploadConfig => uploadConfig.ReleaseId)
                    .Distinct()
                    .ToListAsync(cancellationToken)
            );

            releaseIds.UnionWith(
                await context
                    .Uploads.Where(upload =>
                        upload.HosterRegistrationId != null
                        && deletedHosterRegistrationIds.Contains(upload.HosterRegistrationId.Value)
                    )
                    .Select(upload => upload.UploadConfig.ReleaseId)
                    .Distinct()
                    .ToListAsync(cancellationToken)
            );
        }

        if (deletedArchiveConfigIds.Count > 0)
        {
            releaseIds.UnionWith(
                await context
                    .UploadConfigs.Where(uploadConfig =>
                        deletedArchiveConfigIds.Contains(uploadConfig.ArchiveConfigId)
                    )
                    .Select(uploadConfig => uploadConfig.ReleaseId)
                    .Distinct()
                    .ToListAsync(cancellationToken)
            );
        }

        if (deletedArchiveIds.Count > 0)
        {
            uploadIds.UnionWith(
                await context
                    .UploadedFiles.Where(uploadedFile =>
                        deletedArchiveIds.Contains(uploadedFile.ArchiveFile.ArchiveId)
                    )
                    .Select(uploadedFile => uploadedFile.UploadId)
                    .Distinct()
                    .ToListAsync(cancellationToken)
            );
        }

        if (deletedArchiveFileIds.Count > 0)
        {
            uploadIds.UnionWith(
                await context
                    .UploadedFiles.Where(uploadedFile =>
                        deletedArchiveFileIds.Contains(uploadedFile.ArchiveFileId)
                    )
                    .Select(uploadedFile => uploadedFile.UploadId)
                    .Distinct()
                    .ToListAsync(cancellationToken)
            );
        }
    }

    public List<int> GetReleaseIdsAfterSave()
    {
        return GetIdsAfterSave(releaseIds, releaseIdPropertiesToReadAfterSave);
    }

    public List<int> GetUploadConfigIdsAfterSave()
    {
        return GetIdsAfterSave(uploadConfigIds, uploadConfigIdPropertiesToReadAfterSave);
    }

    public List<int> GetUploadIdsAfterSave()
    {
        return GetIdsAfterSave(uploadIds, uploadIdPropertiesToReadAfterSave);
    }

    private static void AddChangedForeignKey(
        EntityEntry entry,
        string foreignKeyPropertyName,
        List<string> otherPropertyNamesThatChangeTheMaterializedValue,
        HashSet<int> idsKnownBeforeSave,
        List<PropertyEntry> foreignKeyPropertiesToReadAfterSave
    )
    {
        var foreignKeyProperty = entry.Property(foreignKeyPropertyName);

        switch (entry.State)
        {
            case EntityState.Added:
                foreignKeyPropertiesToReadAfterSave.Add(foreignKeyProperty);
                break;
            case EntityState.Deleted:
                idsKnownBeforeSave.Add((int)foreignKeyProperty.OriginalValue!);
                break;
            case EntityState.Modified
                when foreignKeyProperty.IsModified
                    || otherPropertyNamesThatChangeTheMaterializedValue.Any(propertyName =>
                        entry.Property(propertyName).IsModified
                    ):
                idsKnownBeforeSave.Add((int)foreignKeyProperty.OriginalValue!);
                foreignKeyPropertiesToReadAfterSave.Add(foreignKeyProperty);
                break;
        }
    }

    private static List<int> GetIdsAfterSave(
        HashSet<int> idsKnownBeforeSave,
        List<PropertyEntry> foreignKeyPropertiesToReadAfterSave
    )
    {
        return idsKnownBeforeSave
            .Concat(
                foreignKeyPropertiesToReadAfterSave.Select(foreignKeyProperty =>
                    (int)foreignKeyProperty.CurrentValue!
                )
            )
            .Distinct()
            .Order()
            .ToList();
    }
}
