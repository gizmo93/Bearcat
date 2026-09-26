using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.Creation.Repositories;

public interface IReleaseFromFolderPathCreationRepository
{
    Task<ReleaseTemplate?> GetTemplateForReleaseCreationOrDefaultAsync(
        int releaseTemplateId,
        CancellationToken cancellationToken
    );

    void Add(Release release);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
