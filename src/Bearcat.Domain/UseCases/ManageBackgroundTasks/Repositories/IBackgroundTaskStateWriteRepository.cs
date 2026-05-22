using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageBackgroundTasks.Repositories;

public interface IBackgroundTaskStateWriteRepository
{
    Task<BackgroundTaskState?> GetByKeyOrDefaultAsync(
        string key,
        CancellationToken cancellationToken = default
    );

    Task<BackgroundTaskState> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    void Add(BackgroundTaskState taskState);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
