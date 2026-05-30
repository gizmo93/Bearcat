using System.Linq.Expressions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ReleaseReadRepository(IBearcatReadDbContext dbRead, IArchiverFactory archiverFactory)
    : IReleaseReadRepository
{
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
            .Select(c => new ReleaseOverviewUploadConfigProjection(
                c.Id,
                c.Name,
                c.HosterRegistration.Name
            ))
            .ToListAsync(cancellationToken: cancellationToken);

        var latestUploads = await dbRead
            .Uploads.Where(u => u.UploadConfig.ReleaseId == releaseId)
            .GroupBy(u => u.UploadConfigId)
            .Select(g =>
                g.OrderByDescending(u => u.UploadedAt ?? u.CreatedAt)
                    .ThenByDescending(u => u.Id)
                    .Select(u => new ReleaseOverviewLatestUploadProjection(
                        u.UploadConfigId,
                        u.Id,
                        u.CreatedAt,
                        u.UploadedAt,
                        u.UploadState,
                        u.OnlineState,
                        u.UploadedFiles.Count(),
                        u.ErrorMessages.ToList(),
                        u.Archive == null ? null : u.Archive.ArchiveConfig.ArchivePassword
                    ))
                    .First()
            )
            .ToListAsync(cancellationToken: cancellationToken);

        var uploadIds = latestUploads.Select(u => u.UploadId).ToList();
        IReadOnlyList<ReleaseOverviewLinkCrypterLinkProjection> linkCrypterLinks =
            uploadIds.Count == 0
                ? []
                : await dbRead
                    .LinkCrypterContainers.Where(c =>
                        uploadIds.Contains(c.UploadId)
                        && c.Upload.UploadConfig.ReleaseId == releaseId
                    )
                    .OrderBy(c => c.UploadConfigLinkCrypter.LinkCrypterRegistration.Name)
                    .ThenBy(c => c.Id)
                    .Select(c => new ReleaseOverviewLinkCrypterLinkProjection(
                        c.UploadId,
                        c.Id,
                        c.UploadConfigLinkCrypter.LinkCrypterRegistration.Name,
                        c.UploadConfigLinkCrypter.LinkCrypterRegistration.LinkCrypterClassName,
                        c.ContainerUrl,
                        c.State,
                        c.CreatedAt,
                        c.Errors.ToList()
                    ))
                    .ToListAsync(cancellationToken: cancellationToken);

        var latestUploadByConfigId = latestUploads.ToDictionary(u => u.UploadConfigId);
        var linksByUploadId = linkCrypterLinks
            .GroupBy(link => link.UploadId)
            .ToDictionary(
                group => group.Key,
                group =>
                    (IReadOnlyList<ReleaseOverviewLinkCrypterLinkReadModel>)
                        group
                            .Select(link => new ReleaseOverviewLinkCrypterLinkReadModel(
                                link.LinkCrypterContainerId,
                                link.LinkCrypterRegistrationName,
                                link.LinkCrypterClassName,
                                link.ContainerUrl,
                                link.State,
                                link.CreatedAt,
                                link.Errors
                            ))
                            .ToList()
            );

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
                        ar.ArchiveFiles.Count(),
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
                u.UploadedFiles.Count(),
                u.LinkCrypterContainers.Count(),
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
                f.CheckedAt
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
        return await dbRead
            .LinkCrypterContainers.Where(c =>
                c.UploadId == uploadId && c.Upload.UploadConfig.ReleaseId == releaseId
            )
            .OrderBy(c => c.UploadConfigLinkCrypter.LinkCrypterRegistration.Name)
            .ThenBy(c => c.Id)
            .Select(c => new ReleaseUploadContainerLinkReadModel(
                c.UploadConfigLinkCrypter.LinkCrypterRegistration.Name,
                c.UploadConfigLinkCrypter.LinkCrypterRegistration.LinkCrypterClassName,
                c.ContainerUrl,
                c.State,
                c.CreatedAt,
                c.Errors.ToList()
            ))
            .ToListAsync(cancellationToken: cancellationToken);
    }

    private static Expression<Func<Release, ReleaseReadModel>> ToReleaseReadModel()
    {
        return entity => new ReleaseReadModel(
            entity.Id,
            entity.Name,
            entity.ReleaseType,
            entity.ReleaseGroupId,
            entity.ReleaseGroup.Name,
            entity.ReleaseFolderPath,
            entity.UploadConfigs.Count(),
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

        var linksDistributedTo = Normalize(query.LinksDistributedTo);
        if (linksDistributedTo is not null)
        {
            var pattern = ToContainsPattern(linksDistributedTo);
            releases = releases.Where(r =>
                r.UploadConfigs.Any(u =>
                    u.LinksDistributedTo.Any(link => EF.Functions.ILike(link, pattern))
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
                && r.UploadConfigs.Count()
                    == r.UploadConfigs.Count(uc =>
                        uc.Uploads.Any(u => u.OnlineState == OnlineState.Online)
                    )
            ),
            OnlineState.PartiallyOnline => releases.Where(r =>
                r.UploadConfigs.Any(uc => uc.Uploads.Any(u => u.OnlineState == OnlineState.Online))
                && r.UploadConfigs.Any(uc =>
                    !uc.Uploads.Any(u => u.OnlineState == OnlineState.Online)
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

    private record ReleaseOverviewUploadConfigProjection(
        int UploadConfigId,
        string UploadConfigName,
        string HosterRegistrationName
    );

    private record ReleaseOverviewLatestUploadProjection(
        int UploadConfigId,
        int UploadId,
        DateTime CreatedAt,
        DateTime? UploadedAt,
        UploadState UploadState,
        OnlineState OnlineState,
        int LinkCount,
        IReadOnlyList<string> ErrorMessages,
        string? ArchivePassword
    );

    private record ReleaseOverviewLinkCrypterLinkProjection(
        int UploadId,
        int LinkCrypterContainerId,
        string LinkCrypterRegistrationName,
        string LinkCrypterClassName,
        string ContainerUrl,
        LinkCrypterContainerState State,
        DateTime CreatedAt,
        IReadOnlyList<string> Errors
    );
}
