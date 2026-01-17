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
}
