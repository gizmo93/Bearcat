using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.ReadModels;

namespace Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.Repositories;

public interface IReleaseFolderAutomationReadRepository
{
    Task<IReadOnlyList<ReleaseFolderAutomationReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    );
}
