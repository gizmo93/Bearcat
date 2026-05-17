using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseGroups.Dto;
using Bearcat.Domain.UseCases.ManageReleaseGroups.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ReleaseGroupRepository(IBearcatReadDbContext dbRead, IBearcatWriteDbContext dbWrite)
    : IReleaseGroupReadRepository,
        IReleaseGroupWriteRepository
{
    public async Task<IReadOnlyList<ReleaseGroupDto>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ReleaseGroups.OrderBy(r => r.Name)
            .ThenBy(r => r.Id)
            .Select(r => new ReleaseGroupDto(
                r.Id,
                r.Name,
                r.EnableAutomaticReuploads,
                r.NumberOfHoursUntilReupload,
                r.Releases.Count()
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<ReleaseGroupDto?> GetReadModelByIdAsync(
        int releaseGroupId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ReleaseGroups.Where(r => r.Id == releaseGroupId)
            .Select(r => new ReleaseGroupDto(
                r.Id,
                r.Name,
                r.EnableAutomaticReuploads,
                r.NumberOfHoursUntilReupload,
                r.Releases.Count()
            ))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ReleaseGroup> GetByIdAsync(
        int releaseGroupId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite.ReleaseGroups.FirstAsync(
            r => r.Id == releaseGroupId,
            cancellationToken
        );
    }

    public async Task<bool> HasAssignedReleasesAsync(
        int releaseGroupId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite.Releases.AnyAsync(
            r => r.ReleaseGroupId == releaseGroupId,
            cancellationToken
        );
    }

    public void Add(ReleaseGroup releaseGroup)
    {
        dbWrite.Add(releaseGroup);
    }

    public void Remove(ReleaseGroup releaseGroup)
    {
        dbWrite.Remove(releaseGroup);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
