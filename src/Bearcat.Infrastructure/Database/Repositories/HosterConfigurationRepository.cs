using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageHosters.ReadModels;
using Bearcat.Domain.UseCases.ManageHosters.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class HosterConfigurationRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite,
    IHosterFactory hosterFactory
) : IHosterConfigurationReadRepository, IHosterConfigurationWriteRepository
{
    public async Task<IReadOnlyList<HosterRegistrationReadModel>> GetAllRegistrationsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var hostersByName = hosterFactory.GetHostersByName();

        return await dbRead
            .HosterRegistrations.OrderBy(h => h.Name)
            .Select(h => new HosterRegistrationReadModel(
                h.Id,
                h.Name,
                h.IsActive,
                hostersByName[h.HosterClassName].Name,
                h.HosterClassName,
                hostersByName[h.HosterClassName]
                    .DeserializeHosterConfig(h.SerializedConfig)
                    .ToDictionary()
            ))
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<HosterRegistration> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await dbWrite.HosterRegistrations.FirstAsync(h => h.Id == id, cancellationToken);
    }

    public void Add(HosterRegistration registration)
    {
        dbWrite.Add(registration);
    }

    public void Remove(HosterRegistration registration)
    {
        dbWrite.Remove(registration);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
