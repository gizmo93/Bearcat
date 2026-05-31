using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.UseCases.ManageLinkCrypters.ReadModels;
using Bearcat.Domain.UseCases.ManageLinkCrypters.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class LinkCrypterRegistrationReadRepository(
    IBearcatReadDbContext dbRead,
    ILinkCrypterFactory linkCrypterFactory
) : ILinkCrypterRegistrationReadRepository
{
    public async Task<IReadOnlyList<LinkCrypterRegistrationReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        var cryptersByClassName = linkCrypterFactory.GetByClassName();

        var registrations = await dbRead
            .LinkCrypterRegistrations.AsNoTracking()
            .Select(registration => new
            {
                registration.Id,
                registration.Name,
                registration.LinkCrypterClassName,
                registration.IsActive,
            })
            .ToListAsync(cancellationToken);

        return registrations
            .Select(registration =>
            {
                var crypter = cryptersByClassName[registration.LinkCrypterClassName];

                return new LinkCrypterRegistrationReadModel(
                    registration.Id,
                    registration.Name,
                    registration.LinkCrypterClassName,
                    crypter.GetType().Name,
                    registration.IsActive
                );
            })
            .ToList();
    }

    public async Task<LinkCrypterRegistrationReadModel?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var cryptersByClassName = linkCrypterFactory.GetByClassName();

        var registration = await dbRead
            .LinkCrypterRegistrations.AsNoTracking()
            .Where(l => l.Id == id)
            .Select(registration => new
            {
                registration.Id,
                registration.Name,
                registration.LinkCrypterClassName,
                registration.IsActive,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (registration is null)
        {
            return null;
        }

        var crypter = cryptersByClassName[registration.LinkCrypterClassName];

        return new LinkCrypterRegistrationReadModel(
            registration.Id,
            registration.Name,
            registration.LinkCrypterClassName,
            crypter.GetType().Name,
            registration.IsActive
        );
    }
}
