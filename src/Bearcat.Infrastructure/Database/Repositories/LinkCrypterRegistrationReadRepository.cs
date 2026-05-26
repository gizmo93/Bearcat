using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageLinkCrypters.ReadModels;
using Bearcat.Domain.UseCases.ManageLinkCrypters.Repositories;
using Bearcat.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class LinkCrypterRegistrationReadRepository(
    IBearcatReadDbContext dbRead,
    ILinkCrypterFactory linkCrypterFactory,
    ISecretProtector secretProtector
) : ILinkCrypterRegistrationReadRepository
{
    public async Task<IReadOnlyList<LinkCrypterRegistrationReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        var cryptersByClassName = linkCrypterFactory.GetByClassName();

        var registrations = await dbRead.LinkCrypterRegistrations.ToListAsync(cancellationToken);

        return registrations
            .Select(registration => ToReadModel(registration, cryptersByClassName))
            .ToList();
    }

    public async Task<LinkCrypterRegistrationReadModel?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var cryptersByClassName = linkCrypterFactory.GetByClassName();

        var registration = await dbRead
            .LinkCrypterRegistrations.Where(l => l.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        return registration is null ? null : ToReadModel(registration, cryptersByClassName);
    }

    private LinkCrypterRegistrationReadModel ToReadModel(
        LinkCrypterRegistration registration,
        IReadOnlyDictionary<string, ILinkCrypter> cryptersByClassName
    )
    {
        var serializedConfig = secretProtector.Unprotect(registration.SerializedConfig);
        var crypter = cryptersByClassName[registration.LinkCrypterClassName];

        return new LinkCrypterRegistrationReadModel(
            registration.Id,
            registration.Name,
            registration.LinkCrypterClassName,
            crypter.GetType().Name,
            serializedConfig,
            crypter.DeserializeConfig(serializedConfig).ToDictionary(),
            registration.IsActive
        );
    }
}
