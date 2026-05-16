using System.Linq.Expressions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ReleaseReadRepository(IBearcatReadDbContext dbRead, IArchiverFactory archiverFactory)
    : IReleaseReadRepository
{
    public async Task<PagedResult<ReleaseDto>> SearchReleasesAsync(
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
            .Select(ToReleaseDto())
            .ToListAsync(cancellationToken: cancellationToken);

        return new PagedResult<ReleaseDto>(releases, totalCount, pageIndex, pageSize);
    }

    public IReadOnlyList<ArchiverDto> GetArchiverFilterOptions()
    {
        return archiverFactory
            .GetArchivers()
            .OrderBy(a => a.Name)
            .ThenBy(a => a.FileExtension)
            .ToList();
    }

    public async Task<ReleaseDto?> GetReleaseAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .Releases.Where(r => r.Id == releaseId)
            .Select(ToReleaseDto())
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ArchiveConfigDto>> GetArchiveConfigsAsync(
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
            .Select(a => new ArchiveConfigDto(
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
                    .Select(ar => new ArchiveConfigDto.ArchiveSummary(
                        ar.Id,
                        ar.ArchiveFiles.Count()
                    ))
                    .ToList()
            ))
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<PagedResult<ReleaseUploadDto>> SearchUploadsAsync(
        ReleaseUploadSearchQuery query,
        CancellationToken cancellationToken = default
    )
    {
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
            .Select(u => new ReleaseUploadDto(
                u.Id,
                u.UploadConfig.Name,
                u.UploadConfig.HosterRegistration.Name,
                u.CreatedAt,
                u.UploadedAt,
                u.UploadState,
                u.OnlineState,
                u.UploadedFiles.Count()
            ))
            .ToListAsync(cancellationToken: cancellationToken);

        return new PagedResult<ReleaseUploadDto>(uploads, totalCount, pageIndex, pageSize);
    }

    public async Task<PagedResult<ReleaseUploadLinkDto>> SearchUploadLinksAsync(
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
            .Select(f => new ReleaseUploadLinkDto(
                f.ArchiveFile.FullFileName,
                f.HosterFileLink,
                f.OnlineState,
                f.CheckedAt
            ))
            .ToListAsync(cancellationToken: cancellationToken);

        return new PagedResult<ReleaseUploadLinkDto>(links, totalCount, pageIndex, pageSize);
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

    private static Expression<Func<Release, ReleaseDto>> ToReleaseDto()
    {
        return entity => new ReleaseDto(
            entity.Id,
            entity.Name,
            entity.ReleaseType,
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
}
