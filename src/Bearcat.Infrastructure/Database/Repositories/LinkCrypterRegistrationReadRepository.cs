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

        return await dbRead
            .LinkCrypterRegistrations.Select(l => new LinkCrypterRegistrationReadModel(
                l.Id,
                l.Name,
                l.LinkCrypterClassName,
                cryptersByClassName[l.LinkCrypterClassName].GetType().Name,
                l.SerializedConfig,
                cryptersByClassName[l.LinkCrypterClassName]
                    .DeserializeConfig(l.SerializedConfig)
                    .ToDictionary(),
                l.IsActive
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<LinkCrypterRegistrationReadModel?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var cryptersByClassName = linkCrypterFactory.GetByClassName();

        return await dbRead
            .LinkCrypterRegistrations.Where(l => l.Id == id)
            .Select(l => new LinkCrypterRegistrationReadModel(
                l.Id,
                l.Name,
                l.LinkCrypterClassName,
                cryptersByClassName[l.LinkCrypterClassName].GetType().Name,
                l.SerializedConfig,
                cryptersByClassName[l.LinkCrypterClassName]
                    .DeserializeConfig(l.SerializedConfig)
                    .ToDictionary(),
                l.IsActive
            ))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
