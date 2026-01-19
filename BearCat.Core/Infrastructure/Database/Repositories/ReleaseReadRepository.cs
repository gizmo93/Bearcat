using BearCat.Core.Domain.Entities;
using BearCat.Core.Domain.UseCases.ManageReleases.Dto;
using BearCat.Core.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BearCat.Core.Infrastructure.Database.Repositories;

public class ReleaseReadRepository(IBearcatReadDbContext dbRead) : IReleaseReadRepository
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
}
