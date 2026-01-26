using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageReleases.Repositories;

public interface IReleaseWriteRepository
{
    Task<Release> GetByIdAsync(int id, CancellationToken cancellationToken);
    void Add(Release release);
    void Remove(Release release);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
