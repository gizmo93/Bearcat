using System.Linq.Expressions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;
using Bearcat.Domain.UseCases.ReplaceHosters.Dto;
using Bearcat.Domain.UseCases.ReplaceHosters.ReadModels;
using Bearcat.Domain.UseCases.ReplaceHosters.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class HosterReplacementRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite
) : IHosterReplacementRepository
{
    private const int MaximumIdsPerQuery = 1000;

    private static readonly Expression<Func<Upload, bool>> UploadIsUnstarted = upload =>
        (
            upload.UploadState == UploadState.WaitingForArchive
            || upload.UploadState == UploadState.Pending
        ) && !upload.UploadedFiles.Any();

    private static readonly Expression<Func<Upload, bool>> UploadStaysOnSourceHoster = upload =>
        upload.UploadState == UploadState.Uploading
        || upload.UploadState == UploadState.CancellationRequested
        || (
            (
                upload.UploadState == UploadState.WaitingForArchive
                || upload.UploadState == UploadState.Pending
            ) && upload.UploadedFiles.Any()
        );

    public async Task<IReadOnlyList<HosterReplacementUploadTargetReadModel>> GetUploadTargetsAsync(
        IReadOnlyList<UploadTarget> uploadTargets,
        CancellationToken cancellationToken
    )
    {
        var hosterRegistrationIds = GetHosterRegistrationIds(uploadTargets);
        var hosterAccountPoolIds = GetHosterAccountPoolIds(uploadTargets);

        var registrationTargets = await dbRead
            .HosterRegistrations.Where(registration =>
                hosterRegistrationIds.Contains(registration.Id)
            )
            .Select(registration => new HosterReplacementUploadTargetReadModel(
                UploadTarget: UploadTarget.ForHosterRegistration(registration.Id),
                Name: registration.Name,
                HosterClassName: registration.HosterClassName,
                IsUsableForUploads: registration.IsActive
                    && !registration.HasUnreadableSecrets
                    && !registration.RequiresCaptchaVerification
            ))
            .ToListAsync(cancellationToken);

        var poolTargets = await dbRead
            .HosterAccountPools.Where(pool => hosterAccountPoolIds.Contains(pool.Id))
            .Select(pool => new HosterReplacementUploadTargetReadModel(
                UploadTarget: UploadTarget.ForHosterAccountPool(pool.Id),
                Name: pool.Name,
                HosterClassName: pool.HosterClassName,
                IsUsableForUploads: pool.Members.Any(member =>
                    member.HosterRegistration.IsActive
                    && !member.HosterRegistration.HasUnreadableSecrets
                    && !member.HosterRegistration.RequiresCaptchaVerification
                )
            ))
            .ToListAsync(cancellationToken);

        return [.. registrationTargets, .. poolTargets];
    }

    public async Task<
        IReadOnlyList<HosterReplacementUploadConfigRow>
    > GetUploadConfigRowsOfReleasesContainingUploadTargetsAsync(
        IReadOnlyList<UploadTarget> uploadTargets,
        CancellationToken cancellationToken
    )
    {
        var hosterRegistrationIds = GetHosterRegistrationIds(uploadTargets);
        var hosterAccountPoolIds = GetHosterAccountPoolIds(uploadTargets);

        return await dbRead
            .UploadConfigs.Where(uploadConfig =>
                uploadConfig.Release.UploadConfigs.Any(otherUploadConfig =>
                    (
                        otherUploadConfig.HosterRegistrationId != null
                        && hosterRegistrationIds.Contains(
                            otherUploadConfig.HosterRegistrationId.Value
                        )
                    )
                    || (
                        otherUploadConfig.HosterAccountPoolId != null
                        && hosterAccountPoolIds.Contains(
                            otherUploadConfig.HosterAccountPoolId.Value
                        )
                    )
                )
            )
            .OrderBy(uploadConfig => uploadConfig.Id)
            .Select(uploadConfig => new HosterReplacementUploadConfigRow(
                Id: uploadConfig.Id,
                ReleaseId: uploadConfig.ReleaseId,
                ReleaseName: uploadConfig.Release.Name,
                Name: uploadConfig.Name,
                UploadSource: uploadConfig.UploadSource,
                ArchiveConfigId: uploadConfig.ArchiveConfigId,
                CollectionUploadSlotId: uploadConfig.CollectionUploadSlotId,
                HosterRegistrationId: uploadConfig.HosterRegistrationId,
                HosterAccountPoolId: uploadConfig.HosterAccountPoolId,
                HosterClassName: uploadConfig.HosterAccountPoolId == null
                    ? uploadConfig.HosterRegistration!.HosterClassName
                    : uploadConfig.HosterAccountPool!.HosterClassName,
                PremiumOnlyDownload: uploadConfig.PremiumOnlyDownload
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<
        IReadOnlyList<HosterReplacementUploadConfigTemplateRow>
    > GetUploadConfigTemplateRowsOfReleaseTemplatesContainingUploadTargetsAsync(
        IReadOnlyList<UploadTarget> uploadTargets,
        CancellationToken cancellationToken
    )
    {
        var hosterRegistrationIds = GetHosterRegistrationIds(uploadTargets);
        var hosterAccountPoolIds = GetHosterAccountPoolIds(uploadTargets);

        return await dbRead
            .UploadConfigTemplates.Where(template =>
                template.ReleaseTemplate.UploadConfigTemplates.Any(otherTemplate =>
                    (
                        otherTemplate.HosterRegistrationId != null
                        && hosterRegistrationIds.Contains(otherTemplate.HosterRegistrationId.Value)
                    )
                    || (
                        otherTemplate.HosterAccountPoolId != null
                        && hosterAccountPoolIds.Contains(otherTemplate.HosterAccountPoolId.Value)
                    )
                )
            )
            .OrderBy(template => template.Id)
            .Select(template => new HosterReplacementUploadConfigTemplateRow(
                Id: template.Id,
                ReleaseTemplateId: template.ReleaseTemplateId,
                ReleaseTemplateName: template.ReleaseTemplate.Name,
                Name: template.Name,
                UploadSource: template.UploadSource,
                ArchiveConfigTemplateId: template.ArchiveConfigTemplateId,
                HosterRegistrationId: template.HosterRegistrationId,
                HosterAccountPoolId: template.HosterAccountPoolId,
                HosterClassName: template.HosterAccountPoolId == null
                    ? template.HosterRegistration!.HosterClassName
                    : template.HosterAccountPool!.HosterClassName,
                PremiumOnlyDownload: template.PremiumOnlyDownload
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetUnstartedUploadCountAsync(
        IReadOnlyList<int> uploadConfigIds,
        CancellationToken cancellationToken
    )
    {
        return await CountUploadsOfUploadConfigsAsync(
            uploadConfigIds: uploadConfigIds,
            uploadFilter: UploadIsUnstarted,
            cancellationToken: cancellationToken
        );
    }

    public async Task<int> GetUploadCountStayingOnSourceHosterAsync(
        IReadOnlyList<int> uploadConfigIds,
        CancellationToken cancellationToken
    )
    {
        return await CountUploadsOfUploadConfigsAsync(
            uploadConfigIds: uploadConfigIds,
            uploadFilter: UploadStaysOnSourceHoster,
            cancellationToken: cancellationToken
        );
    }

    public async Task<int> GetUploadConfigCountWithoutArchiveSourceAsync(
        IReadOnlyList<int> uploadConfigIds,
        string sourceHosterClassName,
        IReadOnlyList<string> hosterClassNamesWithDownload,
        CancellationToken cancellationToken
    )
    {
        var uploadConfigCount = 0;

        foreach (var uploadConfigIdChunk in uploadConfigIds.Chunk(MaximumIdsPerQuery))
        {
            uploadConfigCount += await dbRead.UploadConfigs.CountAsync(
                uploadConfig =>
                    uploadConfigIdChunk.Contains(uploadConfig.Id)
                    && uploadConfig.ArchiveConfigId != null
                    && !uploadConfig.ArchiveConfig!.Archives.Any(archive =>
                        archive.ArchiveState == ArchiveState.Created
                    )
                    && !(
                        uploadConfig.Release.ReleaseType == ReleaseType.Managed
                        && !string.IsNullOrWhiteSpace(uploadConfig.Release.ReleaseFolderPath)
                    )
                    && !uploadConfig
                        .ArchiveConfig!.UploadConfigs.SelectMany(otherUploadConfig =>
                            otherUploadConfig.Uploads
                        )
                        .Any(upload =>
                            upload.UploadState == UploadState.Completed
                            && upload.HosterRegistration!.IsActive
                            && upload.HosterRegistration.UseForMirrorDownloads
                            && upload.HosterRegistration.HosterClassName != sourceHosterClassName
                            && hosterClassNamesWithDownload.Contains(
                                upload.HosterRegistration.HosterClassName
                            )
                            && upload.UploadedFiles.Any()
                            && upload.UploadedFiles.All(file =>
                                file.OnlineState == OnlineState.Online
                                && !string.IsNullOrWhiteSpace(file.HosterFileLink)
                            )
                        ),
                cancellationToken
            );
        }

        return uploadConfigCount;
    }

    public async Task<
        IReadOnlyList<SourceAccountInUnselectedPoolReadModel>
    > GetSourceAccountsInUnselectedPoolsAsync(
        IReadOnlyList<UploadTarget> sourceUploadTargets,
        CancellationToken cancellationToken
    )
    {
        var hosterRegistrationIds = GetHosterRegistrationIds(sourceUploadTargets);
        var hosterAccountPoolIds = GetHosterAccountPoolIds(sourceUploadTargets);

        return await dbRead
            .HosterAccountPoolMembers.Where(member =>
                hosterRegistrationIds.Contains(member.HosterRegistrationId)
                && !hosterAccountPoolIds.Contains(member.HosterAccountPoolId)
            )
            .OrderBy(member => member.HosterAccountPool.Name)
            .ThenBy(member => member.HosterRegistration.Name)
            .Select(member => new SourceAccountInUnselectedPoolReadModel(
                HosterAccountPoolId: member.HosterAccountPoolId,
                HosterAccountPoolName: member.HosterAccountPool.Name,
                HosterRegistrationId: member.HosterRegistrationId,
                HosterRegistrationName: member.HosterRegistration.Name
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> ApplyChangesAndGetChangedUploadCountAsync(
        HosterReplacementChanges changes,
        CancellationToken cancellationToken
    )
    {
        await using var transaction = await dbWrite.Database.BeginTransactionAsync(
            cancellationToken
        );

        await ApplyChangesToUploadConfigsAsync(changes, cancellationToken);
        await ApplyChangesToUploadConfigTemplatesAsync(changes, cancellationToken);
        var changedUploadCount = await MoveUnstartedUploadsToReplacementAsync(
            changes,
            cancellationToken
        );

        await transaction.CommitAsync(cancellationToken);

        return changedUploadCount;
    }

    private async Task ApplyChangesToUploadConfigsAsync(
        HosterReplacementChanges changes,
        CancellationToken cancellationToken
    )
    {
        var replacement = changes.ReplacementUploadTarget;

        foreach (
            var uploadConfigIdChunk in changes.ReplacedUploadConfigIds.Chunk(MaximumIdsPerQuery)
        )
        {
            await dbWrite
                .UploadConfigs.Where(uploadConfig => uploadConfigIdChunk.Contains(uploadConfig.Id))
                .ExecuteUpdateAsync(
                    setters =>
                        setters
                            .SetProperty(
                                uploadConfig => uploadConfig.HosterRegistrationId,
                                replacement.HosterRegistrationId
                            )
                            .SetProperty(
                                uploadConfig => uploadConfig.HosterAccountPoolId,
                                replacement.HosterAccountPoolId
                            ),
                    cancellationToken
                );
        }

        foreach (
            var uploadConfigIdChunk in changes.RenamedUploadConfigIds.Chunk(MaximumIdsPerQuery)
        )
        {
            await dbWrite
                .UploadConfigs.Where(uploadConfig => uploadConfigIdChunk.Contains(uploadConfig.Id))
                .ExecuteUpdateAsync(
                    setters =>
                        setters.SetProperty(
                            uploadConfig => uploadConfig.Name,
                            changes.ReplacementUploadTargetName
                        ),
                    cancellationToken
                );
        }

        foreach (
            var uploadConfigIdChunk in changes.UploadConfigIdsToResetPremiumOnlyDownload.Chunk(
                MaximumIdsPerQuery
            )
        )
        {
            await dbWrite
                .UploadConfigs.Where(uploadConfig => uploadConfigIdChunk.Contains(uploadConfig.Id))
                .ExecuteUpdateAsync(
                    setters =>
                        setters.SetProperty(
                            uploadConfig => uploadConfig.PremiumOnlyDownload,
                            false
                        ),
                    cancellationToken
                );
        }
    }

    private async Task ApplyChangesToUploadConfigTemplatesAsync(
        HosterReplacementChanges changes,
        CancellationToken cancellationToken
    )
    {
        var replacement = changes.ReplacementUploadTarget;

        foreach (
            var templateIdChunk in changes.ReplacedUploadConfigTemplateIds.Chunk(MaximumIdsPerQuery)
        )
        {
            await dbWrite
                .UploadConfigTemplates.Where(template => templateIdChunk.Contains(template.Id))
                .ExecuteUpdateAsync(
                    setters =>
                        setters
                            .SetProperty(
                                template => template.HosterRegistrationId,
                                replacement.HosterRegistrationId
                            )
                            .SetProperty(
                                template => template.HosterAccountPoolId,
                                replacement.HosterAccountPoolId
                            ),
                    cancellationToken
                );
        }

        foreach (
            var templateIdChunk in changes.RenamedUploadConfigTemplateIds.Chunk(MaximumIdsPerQuery)
        )
        {
            await dbWrite
                .UploadConfigTemplates.Where(template => templateIdChunk.Contains(template.Id))
                .ExecuteUpdateAsync(
                    setters =>
                        setters.SetProperty(
                            template => template.Name,
                            changes.ReplacementUploadTargetName
                        ),
                    cancellationToken
                );
        }

        foreach (
            var templateIdChunk in changes.UploadConfigTemplateIdsToResetPremiumOnlyDownload.Chunk(
                MaximumIdsPerQuery
            )
        )
        {
            await dbWrite
                .UploadConfigTemplates.Where(template => templateIdChunk.Contains(template.Id))
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(template => template.PremiumOnlyDownload, false),
                    cancellationToken
                );
        }
    }

    private async Task<int> MoveUnstartedUploadsToReplacementAsync(
        HosterReplacementChanges changes,
        CancellationToken cancellationToken
    )
    {
        var changedUploadCount = 0;

        foreach (
            var uploadConfigIdChunk in changes.ReplacedUploadConfigIds.Chunk(MaximumIdsPerQuery)
        )
        {
            changedUploadCount += await dbWrite
                .Uploads.Where(upload => uploadConfigIdChunk.Contains(upload.UploadConfigId))
                .Where(UploadIsUnstarted)
                .ExecuteUpdateAsync(
                    setters =>
                    {
                        setters
                            .SetProperty(
                                upload => upload.HosterRegistrationId,
                                changes.ReplacementUploadTarget.HosterRegistrationId
                            )
                            .SetProperty(upload => upload.HosterFolderId, (string?)null);

                        if (!changes.ReplacementHosterSupportsPremiumOnlyDownloads)
                        {
                            setters.SetProperty(upload => upload.PremiumOnlyDownload, false);
                        }
                    },
                    cancellationToken
                );
        }

        return changedUploadCount;
    }

    private async Task<int> CountUploadsOfUploadConfigsAsync(
        IReadOnlyList<int> uploadConfigIds,
        Expression<Func<Upload, bool>> uploadFilter,
        CancellationToken cancellationToken
    )
    {
        var uploadCount = 0;

        foreach (var uploadConfigIdChunk in uploadConfigIds.Chunk(MaximumIdsPerQuery))
        {
            uploadCount += await dbRead
                .Uploads.Where(upload => uploadConfigIdChunk.Contains(upload.UploadConfigId))
                .Where(uploadFilter)
                .CountAsync(cancellationToken);
        }

        return uploadCount;
    }

    private static List<int> GetHosterRegistrationIds(IReadOnlyList<UploadTarget> uploadTargets)
    {
        return uploadTargets
            .Where(target => target.HosterRegistrationId is not null)
            .Select(target => target.HosterRegistrationId!.Value)
            .ToList();
    }

    private static List<int> GetHosterAccountPoolIds(IReadOnlyList<UploadTarget> uploadTargets)
    {
        return uploadTargets
            .Where(target => target.HosterAccountPoolId is not null)
            .Select(target => target.HosterAccountPoolId!.Value)
            .ToList();
    }
}
