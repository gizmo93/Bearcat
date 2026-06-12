using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageBackgroundTasks.ReadModels;
using Bearcat.Domain.UseCases.ManageBackgroundTasks.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class BackgroundTaskStateRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite
) : IBackgroundTaskStateReadRepository, IBackgroundTaskStateWriteRepository
{
    public async Task<IReadOnlyList<BackgroundTaskStateReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .BackgroundTaskStates.OrderBy(t => t.DisplayName)
            .Select(t => new BackgroundTaskStateReadModel(
                t.Id,
                t.Key,
                t.DisplayName,
                t.IsEnabled,
                t.DefaultInterval,
                t.IntervalOverride,
                t.LastStartedAt,
                t.LastFinishedAt,
                t.LastExecutionStatus,
                t.LastErrorMessage,
                t.UpdatedAt
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<BackgroundTaskState?> GetByKeyOrDefaultAsync(
        string key,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite.BackgroundTaskStates.FirstOrDefaultAsync(
            t => t.Key == key,
            cancellationToken
        );
    }

    public async Task<BackgroundTaskState> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite.BackgroundTaskStates.FirstAsync(t => t.Id == id, cancellationToken);
    }

    public void Add(BackgroundTaskState taskState)
    {
        dbWrite.Add(taskState);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
