using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageLinkCrypters.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class LinkCrypterRegistrationWriteRepository(IBearcatWriteDbContext dbWrite)
    : ILinkCrypterRegistrationWriteRepository
{
    public async Task<LinkCrypterRegistration> GetByIdAsync(
        int id,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite.LinkCrypterRegistrations.FirstAsync(
            l => l.Id == id,
            cancellationToken
        );
    }

    public void Add(LinkCrypterRegistration registration)
    {
        dbWrite.LinkCrypterRegistrations.Add(registration);
    }

    public void Remove(LinkCrypterRegistration registration)
    {
        dbWrite.LinkCrypterRegistrations.Remove(registration);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
