using System.Linq.Expressions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.Media;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.Shared.PostQueue;
using Bearcat.Domain.UseCases.ManageImageUploads.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ReleaseReadRepository(
    IBearcatReadDbContext dbRead,
    IArchiverFactory archiverFactory,
    ILinkCrypterFactory linkCrypterFactory
) : IReleaseReadRepository, IReleaseForumPostUploadRepository, IForumPostImageLinkRepository
{
    private static readonly Expression<Func<Release, bool>> IsReadyForPostQueue = r =>
        r.UploadConfigs.Any(uc =>
            uc.CollectionUploadSlotId == null
            && uc.HosterRegistration.IsActive
            && uc.Uploads.Any(u =>
                u.UploadState == UploadState.Completed
                && u.UploadedAt != null
                && (r.UploadsPostedAt == null || u.UploadedAt > r.UploadsPostedAt)
            )
        )
        && r.UploadConfigs.Where(uc =>
                uc.CollectionUploadSlotId == null && uc.HosterRegistration.IsActive
            )
            .All(uc =>
                uc.Uploads.OrderByDescending(u => u.UploadedAt ?? u.CreatedAt)
                    .ThenByDescending(u => u.Id)
                    .Select(u => u.UploadState)
                    .FirstOrDefault() == UploadState.Completed
                && uc.LinkCrypters.Where(lc =>
                        lc.ContainerScope == LinkCrypterContainerScope.Release
                        && lc.LinkCrypterRegistration.IsActive
                    )
                    .All(lc => lc.LinkCrypterContainers.Any())
            )
        && r.ImageUploadConfigs.Where(ic => ic.ImageHosterRegistration.IsActive)
            .All(ic => ic.ImageUploads.Any());

    public async Task<PagedResult<ReleaseReadModel>> SearchReleasesAsync(
        ReleaseSearchQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var pageSize = Math.Clamp(query.PageSize, 5, 100);
        var pageIndex = Math.Max(0, query.PageIndex);

        var releasesQuery = ApplyReleaseSearch(dbRead.Releases, query);
        var totalCount = await releasesQuery.CountAsync(cancellationToken);

        var releases = await releasesQuery
            .OrderBy(r => r.Name)
            .ThenBy(r => r.Id)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .Select(ToReleaseReadModel())
            .ToListAsync(cancellationToken: cancellationToken);

        return new PagedResult<ReleaseReadModel>(releases, totalCount, pageIndex, pageSize);
    }

    public IReadOnlyList<ArchiverDto> GetArchiverFilterOptions()
    {
        return archiverFactory
            .GetArchivers()
            .OrderBy(a => a.Name)
            .ThenBy(a => a.FileExtension)
            .ToList();
    }

    public async Task<ReleaseReadModel?> GetReleaseAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .Releases.Where(r => r.Id == releaseId)
            .Select(ToReleaseReadModel())
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ReleaseOverviewUploadReadModel>> GetReleaseOverviewAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var uploadConfigs = await dbRead
            .UploadConfigs.Where(c => c.ReleaseId == releaseId)
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Select(c => new
            {
                UploadConfigId = c.Id,
                UploadConfigName = c.Name,
                HosterRegistrationName = c.HosterRegistration.Name,
            })
            .ToListAsync(cancellationToken: cancellationToken);

        var latestUploads = await dbRead
            .Uploads.Where(u => u.UploadConfig.ReleaseId == releaseId)
            .GroupBy(u => u.UploadConfigId)
            .Select(g =>
                g.OrderByDescending(u => u.UploadedAt ?? u.CreatedAt)
                    .ThenByDescending(u => u.Id)
                    .Select(u => new
                    {
                        u.UploadConfigId,
                        UploadId = u.Id,
                        u.CreatedAt,
                        u.UploadedAt,
                        u.UploadState,
                        u.OnlineState,
                        LinkCount = u.UploadedFiles.Count,
                        ErrorMessages = u.ErrorMessages.ToList(),
                        ArchivePassword = u.Archive == null
                            ? null
                            : u.Archive.ArchiveConfig.ArchivePassword,
                    })
                    .First()
            )
            .ToListAsync(cancellationToken: cancellationToken);

        var uploadIds = latestUploads.Select(u => u.UploadId).ToList();
        var linksByUploadId = new Dictionary<int, List<ReleaseOverviewLinkCrypterLinkReadModel>>();

        if (uploadIds.Count > 0)
        {
            var releaseContainerLinks = await dbRead
                .LinkCrypterContainers.Where(c =>
                    c.Scope == LinkCrypterContainerScope.Release
                    && c.UploadId != null
                    && uploadIds.Contains(c.UploadId.Value)
                    && c.Upload!.UploadConfig.ReleaseId == releaseId
                )
                .Select(c => new
                {
                    UploadId = c.UploadId!.Value,
                    LinkCrypterContainerId = c.Id,
                    LinkCrypterRegistrationName = c.LinkCrypterRegistration.Name,
                    LinkCrypterClassName = c.LinkCrypterRegistration.LinkCrypterClassName,
                    c.ContainerUrl,
                    c.Scope,
                    c.State,
                    c.CreatedAt,
                    Errors = c.Errors.ToList(),
                })
                .ToListAsync(cancellationToken: cancellationToken);

            var collectionContainerLinks = await dbRead
                .LinkCrypterContainerSourceUploads.Where(source =>
                    uploadIds.Contains(source.UploadId)
                    && source.Upload.UploadConfig.ReleaseId == releaseId
                    && source.LinkCrypterContainer.Scope
                        == LinkCrypterContainerScope.ReleaseCollection
                )
                .Select(source => new
                {
                    source.UploadId,
                    LinkCrypterContainerId = source.LinkCrypterContainer.Id,
                    LinkCrypterRegistrationName = source
                        .LinkCrypterContainer
                        .LinkCrypterRegistration
                        .Name,
                    LinkCrypterClassName = source
                        .LinkCrypterContainer
                        .LinkCrypterRegistration
                        .LinkCrypterClassName,
                    source.LinkCrypterContainer.ContainerUrl,
                    source.LinkCrypterContainer.Scope,
                    source.LinkCrypterContainer.State,
                    source.LinkCrypterContainer.CreatedAt,
                    Errors = source.LinkCrypterContainer.Errors.ToList(),
                })
                .ToListAsync(cancellationToken: cancellationToken);

            linksByUploadId = releaseContainerLinks
                .Concat(collectionContainerLinks)
                .OrderBy(link => link.LinkCrypterRegistrationName)
                .ThenBy(link => link.LinkCrypterContainerId)
                .GroupBy(link => link.UploadId)
                .ToDictionary(
                    group => group.Key,
                    group =>
                        group
                            .Select(link => new ReleaseOverviewLinkCrypterLinkReadModel(
                                LinkCrypterContainerId: link.LinkCrypterContainerId,
                                LinkCrypterRegistrationName: link.LinkCrypterRegistrationName,
                                LinkCrypterClassName: link.LinkCrypterClassName,
                                ContainerUrl: link.ContainerUrl,
                                Scope: link.Scope,
                                State: link.State,
                                CreatedAt: link.CreatedAt,
                                Errors: link.Errors
                            ))
                            .ToList()
                );
        }

        var latestUploadByConfigId = latestUploads.ToDictionary(u => u.UploadConfigId);

        return uploadConfigs
            .Select(config =>
            {
                latestUploadByConfigId.TryGetValue(config.UploadConfigId, out var upload);

                IReadOnlyList<ReleaseOverviewLinkCrypterLinkReadModel> links =
                    upload is not null
                    && linksByUploadId.TryGetValue(upload.UploadId, out var uploadLinks)
                        ? uploadLinks
                        : [];

                return new ReleaseOverviewUploadReadModel(
                    config.UploadConfigId,
                    config.UploadConfigName,
                    config.HosterRegistrationName,
                    upload?.UploadId,
                    upload?.CreatedAt,
                    upload?.UploadedAt,
                    upload?.UploadState,
                    upload?.OnlineState,
                    upload?.LinkCount ?? 0,
                    upload?.ErrorMessages ?? [],
                    upload?.ArchivePassword,
                    links
                );
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ReleasePostQueueItemReadModel>> GetPostQueueAsync(
        CancellationToken cancellationToken = default
    )
    {
        var openReleases = await dbRead
            .Releases.Where(IsReadyForPostQueue)
            .Select(r => new
            {
                r.Id,
                r.Name,
                LatestUploadedAt = r
                    .UploadConfigs.Where(uc => uc.CollectionUploadSlotId == null)
                    .SelectMany(uc => uc.Uploads)
                    .Where(u => u.UploadState == UploadState.Completed && u.UploadedAt != null)
                    .Max(u => u.UploadedAt),
            })
            .ToListAsync(cancellationToken);

        if (openReleases.Count == 0)
        {
            return [];
        }

        var openReleaseIds = openReleases.Select(r => r.Id).ToList();

        var latestUploads = await dbRead
            .Uploads.Where(u =>
                u.UploadConfig.CollectionUploadSlotId == null
                && openReleaseIds.Contains(u.UploadConfig.ReleaseId)
            )
            .GroupBy(u => u.UploadConfigId)
            .Select(g =>
                g.OrderByDescending(u => u.UploadedAt ?? u.CreatedAt)
                    .ThenByDescending(u => u.Id)
                    .Select(u => new
                    {
                        u.UploadConfig.ReleaseId,
                        ArchiveConfigName = u.UploadConfig.ArchiveConfig.Name,
                        HosterRegistrationName = u.UploadConfig.HosterRegistration.Name,
                        UploadId = u.Id,
                        LinkCount = u.UploadedFiles.Count,
                    })
                    .First()
            )
            .ToListAsync(cancellationToken);

        var uploadIds = latestUploads.Select(u => u.UploadId).ToList();

        var containerRows = await dbRead
            .LinkCrypterContainers.Where(c =>
                c.Scope == LinkCrypterContainerScope.Release
                && c.UploadId != null
                && uploadIds.Contains(c.UploadId.Value)
            )
            .Select(c => new
            {
                ReleaseId = c.Upload!.UploadConfig.ReleaseId,
                LinkCrypterRegistrationName = c.LinkCrypterRegistration.Name,
            })
            .ToListAsync(cancellationToken);

        var archiveGroupsByReleaseId = latestUploads
            .GroupBy(u => u.ReleaseId)
            .ToDictionary(
                releaseGroup => releaseGroup.Key,
                releaseGroup =>
                    releaseGroup
                        .GroupBy(u => u.ArchiveConfigName)
                        .OrderBy(archiveGroup => archiveGroup.Key)
                        .Select(archiveGroup => new ReleasePostQueueArchiveGroupReadModel(
                            ArchiveConfigName: archiveGroup.Key,
                            Hosters: archiveGroup
                                .GroupBy(u => u.HosterRegistrationName)
                                .OrderBy(hosterGroup => hosterGroup.Key)
                                .Select(hosterGroup => new PostQueueHosterReadModel(
                                    HosterRegistrationName: hosterGroup.Key,
                                    LinkCount: hosterGroup.Sum(u => u.LinkCount)
                                ))
                                .ToList()
                        ))
                        .ToList()
            );

        var containersByReleaseId = containerRows
            .GroupBy(c => c.ReleaseId)
            .ToDictionary(
                releaseGroup => releaseGroup.Key,
                releaseGroup =>
                    releaseGroup
                        .GroupBy(c => c.LinkCrypterRegistrationName)
                        .OrderBy(registrationGroup => registrationGroup.Key)
                        .Select(registrationGroup => new PostQueueContainerReadModel(
                            LinkCrypterRegistrationName: registrationGroup.Key,
                            Count: registrationGroup.Count()
                        ))
                        .ToList()
            );

        return openReleases
            .OrderByDescending(r => r.LatestUploadedAt)
            .ThenBy(r => r.Name)
            .Select(r => new ReleasePostQueueItemReadModel(
                ReleaseId: r.Id,
                ReleaseName: r.Name,
                LatestUploadedAt: r.LatestUploadedAt!.Value,
                ArchiveGroups: archiveGroupsByReleaseId.GetValueOrDefault(r.Id, []),
                Containers: containersByReleaseId.GetValueOrDefault(r.Id, [])
            ))
            .ToList();
    }

    public async Task<int> CountPostQueueAsync(CancellationToken cancellationToken = default)
    {
        return await dbRead.Releases.CountAsync(IsReadyForPostQueue, cancellationToken);
    }

    public async Task<
        IReadOnlyList<ReleaseQualityIssueQueueItemReadModel>
    > GetQualityIssuesQueueAsync(CancellationToken cancellationToken = default)
    {
        return await dbRead
            .Releases.Where(r => r.QualityGateState == QualityGateState.Failed)
            .OrderBy(r => r.Name)
            .ThenBy(r => r.Id)
            .Select(r => new ReleaseQualityIssueQueueItemReadModel(
                r.Id,
                r.Name,
                r.ReleaseGroup.Name,
                r.QualityGateEvaluatedAt,
                r.QualityIssues.Select(issue => issue.Description).ToList()
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountQualityIssuesQueueAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead.Releases.CountAsync(
            r => r.QualityGateState == QualityGateState.Failed,
            cancellationToken
        );
    }

    public async Task<IReadOnlyList<ReleaseForumPostUploadReadModel>> GetForumPostUploadsAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var uploadConfigs = await dbRead
            .UploadConfigs.Where(c => c.ReleaseId == releaseId)
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Select(c => new
            {
                UploadConfigId = c.Id,
                UploadConfigName = c.Name,
                HosterName = c.HosterRegistration.Name,
                ArchiverName = c.ArchiveConfig.ArchiverName,
                ArchivePassword = c.ArchiveConfig.ArchivePassword,
                LinkCrypters = c
                    .LinkCrypters.Select(linkCrypter => new
                    {
                        linkCrypter.LinkCrypterRegistrationId,
                        Name = linkCrypter.LinkCrypterRegistration.Name,
                        linkCrypter.Password,
                    })
                    .ToList(),
            })
            .ToListAsync(cancellationToken: cancellationToken);

        var latestUploads = await dbRead
            .Uploads.Where(u => u.UploadConfig.ReleaseId == releaseId)
            .GroupBy(u => u.UploadConfigId)
            .Select(g =>
                g.OrderByDescending(u => u.UploadedAt ?? u.CreatedAt)
                    .ThenByDescending(u => u.Id)
                    .Select(u => new
                    {
                        u.UploadConfigId,
                        UploadId = u.Id,
                        u.UploadedAt,
                    })
                    .First()
            )
            .ToListAsync(cancellationToken: cancellationToken);

        var latestUploadByConfigId = latestUploads.ToDictionary(u => u.UploadConfigId);
        var uploadIds = latestUploads.Select(u => u.UploadId).ToList();

        var linksByUploadId = new Dictionary<int, IReadOnlyList<string>>();
        var containers =
            new List<(
                int UploadId,
                int LinkCrypterRegistrationId,
                string ContainerUrl,
                string? StatusImageId,
                DateTime CreatedAt
            )>();

        if (uploadIds.Count > 0)
        {
            var directLinks = await dbRead
                .UploadedFiles.Where(f =>
                    uploadIds.Contains(f.UploadId) && f.Upload.UploadConfig.ReleaseId == releaseId
                )
                .OrderBy(f => f.ArchiveFile.FullFileName)
                .ThenBy(f => f.Id)
                .Select(f => new { f.UploadId, f.HosterFileLink })
                .ToListAsync(cancellationToken: cancellationToken);

            linksByUploadId = directLinks
                .GroupBy(link => link.UploadId)
                .ToDictionary(
                    group => group.Key,
                    group =>
                        (IReadOnlyList<string>)group.Select(link => link.HosterFileLink).ToList()
                );

            var releaseContainers = await dbRead
                .LinkCrypterContainers.Where(c =>
                    c.Scope == LinkCrypterContainerScope.Release
                    && c.UploadId != null
                    && uploadIds.Contains(c.UploadId.Value)
                    && c.Upload!.UploadConfig.ReleaseId == releaseId
                )
                .Select(c => new
                {
                    UploadId = c.UploadId!.Value,
                    c.LinkCrypterRegistrationId,
                    c.ContainerUrl,
                    c.StatusImageId,
                    c.CreatedAt,
                })
                .ToListAsync(cancellationToken: cancellationToken);

            var collectionContainers = await dbRead
                .LinkCrypterContainerSourceUploads.Where(source =>
                    uploadIds.Contains(source.UploadId)
                    && source.Upload.UploadConfig.ReleaseId == releaseId
                    && source.LinkCrypterContainer.Scope
                        == LinkCrypterContainerScope.ReleaseCollection
                )
                .Select(source => new
                {
                    source.UploadId,
                    source.LinkCrypterContainer.LinkCrypterRegistrationId,
                    source.LinkCrypterContainer.ContainerUrl,
                    source.LinkCrypterContainer.StatusImageId,
                    source.LinkCrypterContainer.CreatedAt,
                })
                .ToListAsync(cancellationToken: cancellationToken);

            containers.AddRange(
                releaseContainers.Select(c =>
                    (
                        c.UploadId,
                        c.LinkCrypterRegistrationId,
                        c.ContainerUrl,
                        c.StatusImageId,
                        c.CreatedAt
                    )
                )
            );
            containers.AddRange(
                collectionContainers.Select(c =>
                    (
                        c.UploadId,
                        c.LinkCrypterRegistrationId,
                        c.ContainerUrl,
                        c.StatusImageId,
                        c.CreatedAt
                    )
                )
            );
        }

        var archiverNamesByClassName = archiverFactory
            .GetArchivers()
            .ToDictionary(archiver => archiver.ClassName, archiver => archiver.Name);

        return uploadConfigs
            .Select(config =>
            {
                latestUploadByConfigId.TryGetValue(config.UploadConfigId, out var upload);

                IReadOnlyList<string> links =
                    upload is not null && linksByUploadId.TryGetValue(upload.UploadId, out var l)
                        ? l
                        : [];

                var linkCrypters = config
                    .LinkCrypters.Select(linkCrypter =>
                    {
                        var container = upload is null
                            ? default
                            : containers.FirstOrDefault(c =>
                                c.UploadId == upload.UploadId
                                && c.LinkCrypterRegistrationId
                                    == linkCrypter.LinkCrypterRegistrationId
                            );

                        return new ReleaseForumPostLinkCrypterReadModel(
                            Name: linkCrypter.Name,
                            Password: linkCrypter.Password,
                            ContainerUrl: container.ContainerUrl ?? string.Empty,
                            StatusImageId: container.StatusImageId,
                            CreatedAt: container.CreatedAt
                        );
                    })
                    .ToList();

                return new ReleaseForumPostUploadReadModel(
                    UploadConfigName: config.UploadConfigName,
                    HosterName: config.HosterName,
                    ArchiveFormat: archiverNamesByClassName.GetValueOrDefault(
                        config.ArchiverName,
                        config.ArchiverName
                    ),
                    ArchivePassword: config.ArchivePassword,
                    UploadedAt: upload?.UploadedAt,
                    Links: links,
                    LinkCrypters: linkCrypters
                );
            })
            .ToList();
    }

    public async Task<
        IReadOnlyList<ReleaseOverviewImageUploadReadModel>
    > GetReleaseOverviewImageUploadsAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var imageUploadConfigs = await dbRead
            .ImageUploadConfigs.Where(c => c.ReleaseId == releaseId)
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Select(c => new
            {
                ImageUploadConfigId = c.Id,
                ImageUploadConfigName = c.Name,
                c.ImageHosterRegistration.Name,
                LatestImageUpload = c
                    .ImageUploads.OrderByDescending(upload => upload.UploadedAt ?? upload.CreatedAt)
                    .ThenByDescending(upload => upload.Id)
                    .Select(upload => new
                    {
                        ImageUploadId = upload.Id,
                        upload.CreatedAt,
                        upload.UploadedAt,
                        upload.UploadState,
                        ErrorMessages = upload.ErrorMessages.ToList(),
                        ImageUrls = upload
                            .ImageUrls.OrderBy(url => url.ImageSize)
                            .ThenBy(url => url.Id)
                            .Select(url => new { url.ImageSize, url.Url })
                            .ToList(),
                    })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return imageUploadConfigs
            .Select(config =>
            {
                var upload = config.LatestImageUpload;

                var urls = upload is not null
                    ? upload
                        .ImageUrls.Select(url => new ReleaseOverviewImageUploadUrlReadModel(
                            url.ImageSize,
                            url.Url
                        ))
                        .ToList()
                    : [];

                return new ReleaseOverviewImageUploadReadModel(
                    ImageUploadConfigId: config.ImageUploadConfigId,
                    ImageUploadConfigName: config.ImageUploadConfigName,
                    ImageHosterRegistrationName: config.Name,
                    ImageUploadId: upload?.ImageUploadId,
                    CreatedAt: upload?.CreatedAt,
                    UploadedAt: upload?.UploadedAt,
                    UploadState: upload?.UploadState,
                    ErrorMessages: upload?.ErrorMessages ?? [],
                    ImageUrls: urls
                );
            })
            .ToList();
    }

    public Task<IReadOnlyList<ForumPostImageLinkReadModel>> GetReleaseImageLinksAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        return GetImageLinksAsync(config => config.ReleaseId == releaseId, cancellationToken);
    }

    public Task<IReadOnlyList<ForumPostImageLinkReadModel>> GetCollectionImageLinksAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    )
    {
        return GetImageLinksAsync(
            config => config.ReleaseCollectionId == releaseCollectionId,
            cancellationToken
        );
    }

    private async Task<IReadOnlyList<ForumPostImageLinkReadModel>> GetImageLinksAsync(
        Expression<Func<ImageUploadConfig, bool>> predicate,
        CancellationToken cancellationToken
    )
    {
        var configs = await dbRead
            .ImageUploadConfigs.Where(predicate)
            .OrderBy(config => config.Name)
            .ThenBy(config => config.Id)
            .Select(config => new
            {
                config.Name,
                Urls = config
                    .ImageUploads.OrderByDescending(upload => upload.UploadedAt ?? upload.CreatedAt)
                    .ThenByDescending(upload => upload.Id)
                    .Take(1)
                    .SelectMany(upload =>
                        upload
                            .ImageUrls.OrderBy(url => url.ImageSize)
                            .ThenBy(url => url.Id)
                            .Select(url => new { url.ImageSize, url.Url })
                    )
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        return configs
            .Select(config => new ForumPostImageLinkReadModel(
                config.Name,
                config
                    .Urls.Select(url => new ForumPostImageLinkUrlReadModel(url.ImageSize, url.Url))
                    .ToList()
            ))
            .ToList();
    }

    public async Task<ReleaseInfoReadModel?> GetReleaseInfoAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ReleaseInfos.AsSplitQuery()
            .Where(info => info.ReleaseId == releaseId)
            .Select(info => new ReleaseInfoReadModel(
                info.Id,
                info.NfoDatabaseClassName,
                info.ReleaseName,
                info.ReleaseDatabaseUrl,
                info.SizeNumber,
                info.SizeUnit,
                info.VideoType,
                info.AudioType,
                info.Genre,
                info.Description,
                info.CoverUrl,
                info.ReleaseNfo == null
                    ? null
                    : new ReleaseNfoReadModel(
                        info.ReleaseNfo.Id,
                        info.ReleaseNfo.FileName,
                        info.ReleaseNfo.Content
                    ),
                info.ExternalInfos.OrderBy(externalInfo => externalInfo.Id)
                    .Select(externalInfo => new ReleaseExternalInfoReadModel(
                        externalInfo.Id,
                        externalInfo.Type,
                        externalInfo.Title,
                        externalInfo
                            .Urls.Select(url => new ReleaseExternalInfoUrlReadModel(
                                url.Type,
                                url.Url
                            ))
                            .ToList()
                    ))
                    .ToList()
            ))
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    public async Task<ReleaseNfoReadModel?> GetReleaseNfoAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ReleaseInfos.Where(info => info.ReleaseId == releaseId && info.ReleaseNfo != null)
            .OrderBy(info => info.NfoDatabaseClassName)
            .ThenBy(info => info.Id)
            .Select(info => new ReleaseNfoReadModel(
                info.ReleaseNfo!.Id,
                info.ReleaseNfo.FileName,
                info.ReleaseNfo.Content
            ))
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ReleaseMediaFileReadModel>> GetMediaFilesAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var files = await dbRead
            .ReleaseMediaFiles.Where(file => file.ReleaseId == releaseId)
            .OrderBy(file => file.RelativePath)
            .Select(file => new
            {
                file.Id,
                file.RelativePath,
                file.SizeBytes,
                file.MediaInfoJson,
                file.MediaInfoText,
            })
            .ToListAsync(cancellationToken);

        return files
            .Select(file =>
            {
                var metadata = MediaInfoOutputParser.Parse(file.MediaInfoJson);

                return new ReleaseMediaFileReadModel(
                    file.Id,
                    file.RelativePath,
                    file.SizeBytes,
                    metadata?.ContainerFormat,
                    metadata?.Duration,
                    metadata?.VideoStream is null
                        ? null
                        : new ReleaseVideoStreamReadModel(
                            metadata.VideoStream.Index,
                            metadata.VideoStream.Codec,
                            metadata.VideoStream.CodecProfile,
                            metadata.VideoStream.IsDefault,
                            metadata.VideoStream.Language,
                            metadata.VideoStream.Title,
                            metadata.VideoStream.Width,
                            metadata.VideoStream.Height,
                            metadata.VideoStream.Fps,
                            metadata.VideoStream.PixelFormat,
                            metadata.VideoStream.BitrateKbps
                        ),
                    metadata
                        ?.AudioStreams.Select(stream => new ReleaseAudioStreamReadModel(
                            stream.Index,
                            stream.Codec,
                            stream.CodecProfile,
                            stream.IsDefault,
                            stream.Language,
                            stream.Title,
                            stream.SampleRate,
                            stream.ChannelLayout,
                            stream.Channels,
                            stream.BitrateKbps
                        ))
                        .ToList()
                        ?? [],
                    metadata
                        ?.SubtitleStreams.Select(stream => new ReleaseSubtitleStreamReadModel(
                            stream.Index,
                            stream.Codec,
                            stream.IsDefault,
                            stream.Forced,
                            stream.Language,
                            stream.Title
                        ))
                        .ToList()
                        ?? [],
                    file.MediaInfoText
                );
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ArchiveConfigReadModel>> GetArchiveConfigsAsync(
        int releaseId,
        CancellationToken cancellationToken
    )
    {
        var archivers = archiverFactory.GetArchivers();

        var fileExtensionByArchiver = archivers.ToDictionary(
            a => a.ClassName,
            a => a.FileExtension
        );

        var nameByArchiverClassName = archivers.ToDictionary(a => a.ClassName, a => a.Name);

        return await dbRead
            .ArchiveConfigs.AsSplitQuery()
            .Where(a => a.ReleaseId == releaseId)
            .OrderBy(a => a.ArchiverName)
            .Select(a => new ArchiveConfigReadModel(
                a.Id,
                a.ArchiveFilesBasePath,
                a.ArchiverName,
                nameByArchiverClassName[a.ArchiverName],
                a.ArchiveNamePrefix,
                a.ArchivePassword,
                a.ArchiveFileSizeMb,
                fileExtensionByArchiver[a.ArchiverName],
                a.Name,
                a.Archives.OrderByDescending(ar => ar.Id)
                    .Select(ar => new ArchiveConfigReadModel.ArchiveSummary(
                        ar.Id,
                        ar.CreatedAt,
                        ar.ArchiveState,
                        ar.ArchiveFiles.Count,
                        ar.ErrorMessages.ToList()
                    ))
                    .ToList()
            ))
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<PagedResult<ReleaseUploadReadModel>> SearchUploadsAsync(
        ReleaseUploadSearchQuery query,
        CancellationToken cancellationToken = default
    )
    {
        List<UploadState> reuploadBlockingStates =
        [
            UploadState.Pending,
            UploadState.Uploading,
            UploadState.WaitingForArchive,
            UploadState.Failed,
            UploadState.CancellationRequested,
        ];

        var pageSize = Math.Clamp(query.PageSize, 5, 100);
        var pageIndex = Math.Max(0, query.PageIndex);

        var uploadsQuery = dbRead.Uploads.Where(u => u.UploadConfig.ReleaseId == query.ReleaseId);

        if (query.UploadConfigId is not null)
        {
            uploadsQuery = uploadsQuery.Where(u => u.UploadConfigId == query.UploadConfigId.Value);
        }

        var totalCount = await uploadsQuery.CountAsync(cancellationToken);

        var uploads = await uploadsQuery
            .OrderByDescending(u => u.UploadedAt ?? u.CreatedAt)
            .ThenByDescending(u => u.Id)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .Select(u => new ReleaseUploadReadModel(
                u.Id,
                u.UploadConfig.Name,
                u.UploadConfig.HosterRegistration.Name,
                u.CreatedAt,
                u.UploadedAt,
                u.UploadState,
                u.OnlineState,
                u.UploadedFiles.Count,
                u.LinkCrypterContainers.Count
                    + dbRead.LinkCrypterContainerSourceUploads.Count(source =>
                        source.UploadId == u.Id
                        && source.LinkCrypterContainer.Scope
                            == LinkCrypterContainerScope.ReleaseCollection
                    ),
                (
                    u.UploadState == UploadState.Canceled
                    || u.UploadState == UploadState.Failed
                    || u.OnlineState == OnlineState.Offline
                    || u.OnlineState == OnlineState.PartiallyOnline
                )
                    && !u.UploadConfig.Uploads.Any(ru =>
                        ru.Id != u.Id
                        && (
                            ru.OnlineState == OnlineState.Online
                            || reuploadBlockingStates.Contains(ru.UploadState)
                        )
                    ),
                u.ErrorMessages.ToList()
            ))
            .ToListAsync(cancellationToken: cancellationToken);

        return new PagedResult<ReleaseUploadReadModel>(uploads, totalCount, pageIndex, pageSize);
    }

    public async Task<PagedResult<ReleaseUploadLinkReadModel>> SearchUploadLinksAsync(
        ReleaseUploadLinkSearchQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var pageSize = Math.Clamp(query.PageSize, 5, 100);
        var pageIndex = Math.Max(0, query.PageIndex);

        var linksQuery = dbRead.UploadedFiles.Where(f =>
            f.UploadId == query.UploadId && f.Upload.UploadConfig.ReleaseId == query.ReleaseId
        );

        if (query.OnlineState is not null)
        {
            linksQuery = linksQuery.Where(f => f.OnlineState == query.OnlineState.Value);
        }

        var totalCount = await linksQuery.CountAsync(cancellationToken);

        var links = await linksQuery
            .OrderBy(f => f.ArchiveFile.FullFileName)
            .ThenBy(f => f.Id)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .Select(f => new ReleaseUploadLinkReadModel(
                f.ArchiveFile.FullFileName,
                f.HosterFileLink,
                f.OnlineState,
                f.CheckedAt,
                f.ErrorMessages
            ))
            .ToListAsync(cancellationToken: cancellationToken);

        return new PagedResult<ReleaseUploadLinkReadModel>(links, totalCount, pageIndex, pageSize);
    }

    public async Task<IReadOnlyList<string>> GetUploadLinksAsync(
        int releaseId,
        int uploadId,
        OnlineState? onlineState = null,
        CancellationToken cancellationToken = default
    )
    {
        var linksQuery = dbRead.UploadedFiles.Where(f =>
            f.UploadId == uploadId && f.Upload.UploadConfig.ReleaseId == releaseId
        );

        if (onlineState is not null)
        {
            linksQuery = linksQuery.Where(f => f.OnlineState == onlineState.Value);
        }

        return await linksQuery
            .OrderBy(f => f.ArchiveFile.FullFileName)
            .ThenBy(f => f.Id)
            .Select(f => f.HosterFileLink)
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<
        IReadOnlyList<ReleaseUploadContainerLinkReadModel>
    > GetUploadContainerLinksAsync(
        int releaseId,
        int uploadId,
        CancellationToken cancellationToken = default
    )
    {
        var linkCryptersByClassName = linkCrypterFactory
            .GetLinkCrypters()
            .ToDictionary(l => l.ClassName);

        var containers = await dbRead
            .LinkCrypterContainers.Where(c =>
                c.Scope == LinkCrypterContainerScope.Release
                && c.UploadId == uploadId
                && c.Upload!.UploadConfig.ReleaseId == releaseId
            )
            .Select(c => new
            {
                LinkCrypterRegistrationName = c.LinkCrypterRegistration.Name,
                LinkCrypterClassName = c.LinkCrypterRegistration.LinkCrypterClassName,
                c.ContainerUrl,
                c.StatusImageId,
                c.Scope,
                c.State,
                c.CreatedAt,
                c.EnableCaptcha,
                c.EnableContainerDownload,
                c.EnableClickAndLoad,
                Errors = c.Errors.ToList(),
            })
            .ToListAsync(cancellationToken: cancellationToken);

        var collectionContainers = await dbRead
            .LinkCrypterContainerSourceUploads.Where(source =>
                source.UploadId == uploadId
                && source.Upload.UploadConfig.ReleaseId == releaseId
                && source.LinkCrypterContainer.Scope == LinkCrypterContainerScope.ReleaseCollection
            )
            .Select(source => new
            {
                LinkCrypterRegistrationName = source
                    .LinkCrypterContainer
                    .LinkCrypterRegistration
                    .Name,
                LinkCrypterClassName = source
                    .LinkCrypterContainer
                    .LinkCrypterRegistration
                    .LinkCrypterClassName,
                source.LinkCrypterContainer.ContainerUrl,
                source.LinkCrypterContainer.StatusImageId,
                source.LinkCrypterContainer.Scope,
                source.LinkCrypterContainer.State,
                source.LinkCrypterContainer.CreatedAt,
                source.LinkCrypterContainer.EnableCaptcha,
                source.LinkCrypterContainer.EnableContainerDownload,
                source.LinkCrypterContainer.EnableClickAndLoad,
                Errors = source.LinkCrypterContainer.Errors.ToList(),
            })
            .ToListAsync(cancellationToken: cancellationToken);

        return containers
            .Concat(collectionContainers)
            .OrderBy(container => container.LinkCrypterRegistrationName)
            .ThenBy(container => container.CreatedAt)
            .Select(container => new ReleaseUploadContainerLinkReadModel(
                LinkCrypterRegistrationName: container.LinkCrypterRegistrationName,
                LinkCrypterClassName: container.LinkCrypterClassName,
                ContainerUrl: container.ContainerUrl,
                StatusImageId: container.StatusImageId,
                Scope: container.Scope,
                State: container.State,
                CreatedAt: container.CreatedAt,
                EnableCaptcha: container.EnableCaptcha,
                EnableContainerDownload: container.EnableContainerDownload,
                EnableClickAndLoad: container.EnableClickAndLoad,
                SupportsCaptcha: linkCryptersByClassName[
                    container.LinkCrypterClassName
                ].SupportsCaptcha,
                SupportsContainerDownload: linkCryptersByClassName[
                    container.LinkCrypterClassName
                ].SupportsContainerDownload,
                SupportsClickAndLoad: linkCryptersByClassName[
                    container.LinkCrypterClassName
                ].SupportsClickAndLoad,
                Errors: container.Errors
            ))
            .ToList();
    }

    public async Task<IReadOnlyList<ReleaseImageUploadReadModel>> GetImageUploadsAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ImageUploads.Where(upload => upload.ImageUploadConfig.ReleaseId == releaseId)
            .OrderByDescending(upload => upload.UploadedAt ?? upload.CreatedAt)
            .ThenByDescending(upload => upload.Id)
            .Select(upload => new ReleaseImageUploadReadModel(
                upload.Id,
                upload.ImageUploadConfig.Name,
                upload.ImageUploadConfig.ImageHosterRegistration.Name,
                upload.CreatedAt,
                upload.UploadedAt,
                upload.UploadState,
                upload.ImageUrls.Count,
                upload.ErrorMessages.ToList()
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReleaseImageUploadUrlReadModel>> GetImageUploadUrlsAsync(
        int releaseId,
        int imageUploadId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ImageUploadUrls.Where(url =>
                url.ImageUploadId == imageUploadId
                && url.ImageUpload.ImageUploadConfig.ReleaseId == releaseId
            )
            .OrderBy(url => url.ImageSize)
            .ThenBy(url => url.Id)
            .Select(url => new ReleaseImageUploadUrlReadModel(url.ImageSize, url.Url))
            .ToListAsync(cancellationToken);
    }

    private static Expression<Func<Release, ReleaseReadModel>> ToReleaseReadModel()
    {
        return entity => new ReleaseReadModel(
            entity.Id,
            entity.Name,
            entity.ReleaseType,
            entity.ReleaseContentType,
            entity.ReleaseGroupId,
            entity.ReleaseGroup.Name,
            entity.ReleaseFolderPath,
            entity.UploadConfigs.Count,
            entity
                .UploadConfigs.Where(uc => uc.Uploads.Any(u => u.OnlineState == OnlineState.Online))
                .Distinct()
                .Count()
        );
    }

    private static IQueryable<Release> ApplyReleaseSearch(
        IQueryable<Release> releases,
        ReleaseSearchQuery query
    )
    {
        var searchTerm = Normalize(query.SearchTerm);

        if (searchTerm is not null)
        {
            var pattern = ToContainsPattern(searchTerm);

            releases = releases.Where(r =>
                EF.Functions.ILike(r.Name, pattern)
                || EF.Functions.ILike(r.ReleaseFolderPath, pattern)
            );
        }

        if (query.OnlineState is not null)
        {
            releases = ApplyOnlineStateFilter(releases, query.OnlineState.Value);
        }

        if (query.ReleaseType is not null)
        {
            releases = releases.Where(r => r.ReleaseType == query.ReleaseType.Value);
        }

        if (query.ReleaseContentType is not null)
        {
            releases = releases.Where(r => r.ReleaseContentType == query.ReleaseContentType.Value);
        }

        if (query.HosterRegistrationId is not null)
        {
            releases = releases.Where(r =>
                r.UploadConfigs.Any(u => u.HosterRegistrationId == query.HosterRegistrationId.Value)
            );
        }

        var archiverName = Normalize(query.ArchiverName);

        if (archiverName is not null)
        {
            releases = releases.Where(r =>
                r.ArchiveConfigs.Any(a => a.ArchiverName == archiverName)
            );
        }

        if (query.LinkCrypterRegistrationId is not null)
        {
            releases = releases.Where(r =>
                r.UploadConfigs.Any(u =>
                    u.LinkCrypters.Any(l =>
                        l.LinkCrypterRegistrationId == query.LinkCrypterRegistrationId.Value
                    )
                )
            );
        }

        if (query.ReleaseGroupId is not null)
        {
            releases = releases.Where(r => r.ReleaseGroupId == query.ReleaseGroupId.Value);
        }

        var postedLocationUrl = Normalize(query.PostedLocationUrl);

        if (postedLocationUrl is not null)
        {
            var pattern = ToContainsPattern(postedLocationUrl);
            releases = releases.Where(r =>
                r.PostedLocations.Any(location => EF.Functions.ILike(location.Url, pattern))
            );
        }

        var downloadLink = Normalize(query.DownloadLink);

        if (downloadLink is not null)
        {
            var pattern = ToContainsPattern(downloadLink);

            releases = releases.Where(r =>
                r.UploadConfigs.Any(u =>
                    u.Uploads.Any(upload =>
                        upload.UploadedFiles.Any(file =>
                            EF.Functions.ILike(file.HosterFileLink, pattern)
                        )
                    )
                )
            );
        }

        var archiveFileName = Normalize(query.ArchiveFileName);

        if (archiveFileName is not null)
        {
            var pattern = ToContainsPattern(archiveFileName);
            releases = releases.Where(r =>
                r.ArchiveConfigs.Any(config =>
                    config.Archives.Any(archive =>
                        archive.ArchiveFiles.Any(file =>
                            EF.Functions.ILike(file.FullFileName, pattern)
                        )
                    )
                )
            );
        }

        var uploadId = Normalize(query.UploadId)?.TrimStart('#');

        if (uploadId is not null)
        {
            if (!int.TryParse(uploadId, out var parsedUploadId))
            {
                return releases.Where(_ => false);
            }

            releases = releases.Where(r =>
                r.UploadConfigs.Any(config =>
                    config.Uploads.Any(upload => upload.Id == parsedUploadId)
                )
            );
        }

        return releases;
    }

    private static IQueryable<Release> ApplyOnlineStateFilter(
        IQueryable<Release> releases,
        OnlineState onlineState
    )
    {
        return onlineState switch
        {
            OnlineState.Unknown => releases.Where(r => !r.UploadConfigs.Any()),
            OnlineState.Online => releases.Where(r =>
                r.UploadConfigs.Any()
                && r.UploadConfigs.Count
                    == r.UploadConfigs.Count(uc =>
                        uc.Uploads.Any(u => u.OnlineState == OnlineState.Online)
                    )
            ),
            OnlineState.PartiallyOnline => releases.Where(r =>
                r.UploadConfigs.Any(uc => uc.Uploads.Any(u => u.OnlineState == OnlineState.Online))
                && r.UploadConfigs.Any(uc =>
                    uc.Uploads.All(u => u.OnlineState != OnlineState.Online)
                )
            ),
            OnlineState.Offline => releases.Where(r =>
                r.UploadConfigs.Any()
                && !r.UploadConfigs.Any(uc =>
                    uc.Uploads.Any(u => u.OnlineState == OnlineState.Online)
                )
            ),
            _ => releases,
        };
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string ToContainsPattern(string value)
    {
        return $"%{value}%";
    }
}
