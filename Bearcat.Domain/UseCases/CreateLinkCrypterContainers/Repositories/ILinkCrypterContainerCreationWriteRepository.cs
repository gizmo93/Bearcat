using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.CreateLinkCrypterContainers.Repositories;

public interface ILinkCrypterContainerCreationWriteRepository
{
    Task<IReadOnlyList<Upload>> GetUploadsWithMissingLinkCrypterContainersAsync(
        CancellationToken cancellationToken);

    void Add(LinkCrypterContainer container);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
