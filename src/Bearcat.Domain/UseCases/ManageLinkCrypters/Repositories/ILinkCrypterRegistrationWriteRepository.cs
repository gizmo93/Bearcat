using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageLinkCrypters.Repositories;

public interface ILinkCrypterRegistrationWriteRepository
{
    Task<LinkCrypterRegistration> GetByIdAsync(int id, CancellationToken cancellationToken);
    void Add(LinkCrypterRegistration registration);
    void Remove(LinkCrypterRegistration registration);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
