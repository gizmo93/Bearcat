using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageHosterAccountPools.Deletion;
using Bearcat.Domain.UseCases.ManageHosterAccountPools.ReadModels;
using Bearcat.Domain.UseCases.ManageHosterAccountPools.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class HosterAccountPoolRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite,
    IHosterFactory hosterFactory
) : IHosterAccountPoolReadRepository, IHosterAccountPoolWriteRepository
{
    public async Task<IReadOnlyList<HosterAccountPoolReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        var pools = await SelectPoolsWithMembers(
                dbRead.HosterAccountPools.OrderBy(pool => pool.Name).ThenBy(pool => pool.Id)
            )
            .ToListAsync(cancellationToken);

        return pools.Select(ToReadModel).ToList();
    }

    public async Task<HosterAccountPoolReadModel?> GetDetailAsync(
        int hosterAccountPoolId,
        CancellationToken cancellationToken = default
    )
    {
        var pool = await SelectPoolsWithMembers(
                dbRead.HosterAccountPools.Where(pool => pool.Id == hosterAccountPoolId)
            )
            .FirstOrDefaultAsync(cancellationToken);

        return pool is null ? null : ToReadModel(pool);
    }

    public async Task<
        IReadOnlyList<HosterAccountPoolCandidateReadModel>
    > GetCandidateRegistrationsAsync(
        string hosterClassName,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .HosterRegistrations.Where(registration =>
                registration.HosterClassName == hosterClassName
            )
            .OrderBy(registration => registration.Name)
            .ThenBy(registration => registration.Id)
            .Select(registration => new HosterAccountPoolCandidateReadModel(
                registration.Id,
                registration.Name,
                registration.IsActive,
                registration.HasUnreadableSecrets
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetHosterClassNamesWithRegistrationsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .HosterRegistrations.Select(registration => registration.HosterClassName)
            .Distinct()
            .OrderBy(hosterClassName => hosterClassName)
            .ToListAsync(cancellationToken);
    }

    public async Task<HosterAccountPool> GetByIdAsync(
        int hosterAccountPoolId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .HosterAccountPools.Include(pool => pool.Members)
            .FirstAsync(pool => pool.Id == hosterAccountPoolId, cancellationToken);
    }

    public async Task<bool> NameExistsAsync(
        string name,
        int? excludedHosterAccountPoolId,
        CancellationToken cancellationToken
    )
    {
        return await dbRead.HosterAccountPools.AnyAsync(
            pool => pool.Name == name && pool.Id != excludedHosterAccountPoolId,
            cancellationToken
        );
    }

    public async Task<Dictionary<int, string>> GetHosterClassNamesByHosterRegistrationIdAsync(
        IReadOnlyList<int> hosterRegistrationIds,
        CancellationToken cancellationToken
    )
    {
        return await dbRead
            .HosterRegistrations.Where(registration =>
                hosterRegistrationIds.Contains(registration.Id)
            )
            .ToDictionaryAsync(
                registration => registration.Id,
                registration => registration.HosterClassName,
                cancellationToken
            );
    }

    public async Task<HosterAccountPoolUsage> GetUsageAsync(
        int hosterAccountPoolId,
        CancellationToken cancellationToken
    )
    {
        return await dbRead
            .HosterAccountPools.Where(pool => pool.Id == hosterAccountPoolId)
            .Select(pool => new HosterAccountPoolUsage(
                pool.UploadConfigs.Count,
                pool.UploadConfigTemplates.Count
            ))
            .FirstAsync(cancellationToken);
    }

    public void Add(HosterAccountPool hosterAccountPool)
    {
        dbWrite.Add(hosterAccountPool);
    }

    public void Remove(HosterAccountPool hosterAccountPool)
    {
        dbWrite.Remove(hosterAccountPool);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<HosterAccountPoolProjection> SelectPoolsWithMembers(
        IQueryable<HosterAccountPool> pools
    )
    {
        return pools.Select(pool => new HosterAccountPoolProjection(
            pool.Id,
            pool.Name,
            pool.HosterClassName,
            pool.MinimumFreeSpaceBytes,
            pool.Members.OrderBy(member => member.Priority)
                .ThenBy(member => member.Id)
                .Select(member => new HosterAccountPoolMemberReadModel(
                    member.HosterRegistrationId,
                    member.HosterRegistration.Name,
                    member.HosterRegistration.IsActive,
                    member.HosterRegistration.HasUnreadableSecrets,
                    member.Priority
                ))
                .ToList()
        ));
    }

    private HosterAccountPoolReadModel ToReadModel(HosterAccountPoolProjection pool)
    {
        var hoster = hosterFactory.GetByName(pool.HosterClassName);

        return new HosterAccountPoolReadModel(
            pool.Id,
            pool.Name,
            pool.HosterClassName,
            hoster.Name,
            hoster.SupportsPremiumOnlyDownloads,
            hoster is IHosterWithFileSizeLimit fileSizeLimit ? fileSizeLimit.MaxFileSizeMb : null,
            pool.MinimumFreeSpaceBytes,
            pool.MembersInPriorityOrder
        );
    }

    private sealed record HosterAccountPoolProjection(
        int Id,
        string Name,
        string HosterClassName,
        long MinimumFreeSpaceBytes,
        List<HosterAccountPoolMemberReadModel> MembersInPriorityOrder
    );
}
