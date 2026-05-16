using System.Linq.Expressions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ReleaseReadRepository(IBearcatReadDbContext dbRead, IArchiverFactory archiverFactory)
    : IReleaseReadRepository
{
    public async Task<IReadOnlyList<ReleaseDto>> GetReleasesAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .Releases.Select(ToReleaseDto())
            .ToListAsync(cancellationToken: cancellationToken);
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
}
