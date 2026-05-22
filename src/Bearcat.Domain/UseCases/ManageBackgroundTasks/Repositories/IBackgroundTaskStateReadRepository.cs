using Bearcat.Domain.UseCases.ManageBackgroundTasks.ReadModels;

namespace Bearcat.Domain.UseCases.ManageBackgroundTasks.Repositories;

public interface IBackgroundTaskStateReadRepository
{
    Task<IReadOnlyList<BackgroundTaskStateReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    );
}
