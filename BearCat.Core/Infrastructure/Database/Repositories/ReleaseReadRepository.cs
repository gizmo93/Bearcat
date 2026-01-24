using BearCat.Core.Domain.Abstractions.Archiver;
using BearCat.Core.Domain.UseCases.ManageReleases.Dto;
using BearCat.Core.Domain.UseCases.ManageReleases.Repositories;
using BearCat.Core.Domain.UseCases.ManageUploadConfigs.Dto;
using Microsoft.EntityFrameworkCore;

namespace BearCat.Core.Infrastructure.Database.Repositories;

public class ReleaseReadRepository(
    IBearcatReadDbContext dbRead,
    IArchiverFactory archiverFactory)
    : IReleaseReadRepository
{
    public async Task<IReadOnlyList<ReleaseListDto>> GetReleasesAsync(CancellationToken cancellationToken = default)
    {
        return await dbRead
            .Releases
            .Select(r => new ReleaseListDto(
                r.Id,
                r.Name,
                r.ReleaseType,
                r.ArchiveConfigs.Count(),
                r.UploadConfigs.Count(),
                r.ReleaseFolderPath,
                r.UploadConfigs
                    .Select(u =>
                        new ReleaseListDto.UploadConfigDto(
                            u.Name,
                            u.Uploads
                                .OrderByDescending(up => up.Id)
                                .Last()
                                .OnlineState))
                    .ToList()))
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<ReleaseDto?> GetReleaseAsync(int releaseId, CancellationToken cancellationToken = default)
    {
        return await dbRead
            .Releases
            .Where(r => r.Id == releaseId)
            .Select(r => new ReleaseDto(
                r.Id,
                r.Name,
                r.ReleaseType,
                r.ReleaseFolderPath))
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ArchiveConfigDto>> GetArchiveConfigsAsync(int releaseId,
        CancellationToken cancellationToken)
    {
        var archivers = archiverFactory.GetArchivers();

        var fileExtensionByArchiver = archivers
            .ToDictionary(a => a.ClassName, a => a.FileExtension);

        var nameByArchiverClassName = archivers
            .ToDictionary(a => a.ClassName, a => a.Name);

        return await dbRead.ArchiveConfigs
            .AsSplitQuery()
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
                a.Archives
                    .OrderByDescending(ar => ar.Id)
                    .Select(ar => new ArchiveConfigDto.ArchiveSummary(
                        ar.Id,
                        ar.ArchiveFiles.Count()))
                    .ToList()))
            .ToListAsync(cancellationToken: cancellationToken);
    }
}
