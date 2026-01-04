using BearCat.Core.Domain.Abstractions.Hoster;
using BearCat.Core.Domain.Entities;
using BearCat.Core.Domain.UseCases.ManageHosters.Dto;
using BearCat.Core.Domain.UseCases.ManageHosters.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BearCat.Core.Infrastructure.Database.Repositories;

public class HosterConfigurationRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite,
    IEnumerable<IHoster> hosters) : IHosterConfigurationReadRepository, IHosterConfigurationWriteRepository
{
    public async Task<IReadOnlyList<HosterRegistrationDto>> GetAllRegistrationsAsync(
        CancellationToken cancellationToken = default)
    {
        var hosterNamesByClassName = GetHosterNamesByClassName();

        return await dbRead.HosterRegistrations
            .Select(h => new HosterRegistrationDto(
                h.Id,
                h.Name,
                h.IsActive,
                hosterNamesByClassName[h.HosterFullClassName]))
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<HosterRegistration> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await dbRead.HosterRegistrations
            .FirstAsync(h => h.Id == id, cancellationToken);
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

    private Dictionary<string, string> GetHosterNamesByClassName()
    {
        return hosters
            .ToDictionary(h => h.GetType().FullName!, h => h.Name);
    }
}
