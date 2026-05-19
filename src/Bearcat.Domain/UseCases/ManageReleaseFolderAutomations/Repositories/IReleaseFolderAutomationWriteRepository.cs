using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.Repositories;

public interface IReleaseFolderAutomationWriteRepository
{
    void Add(ReleaseFolderAutomation automation);

    void Remove(ReleaseFolderAutomation automation);

    Task<ReleaseFolderAutomation> GetByIdAsync(
        int releaseFolderAutomationId,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
