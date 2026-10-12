using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bearcat.Infrastructure.Database.MaterializedValues;

public sealed class MaterializedValuesSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, PendingUpdate> pendingUpdatesByContext = new();

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result
    )
    {
        throw new NotSupportedException(
            "Synchronous SaveChanges is not supported: Release.OnlineState and Upload.OldestUploadedFileCheckedAt are only kept up to date by SaveChangesAsync."
        );
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        var context = (BearcatDbContext)eventData.Context!;
        var releasesAndUploadsToUpdate = ReleasesAndUploadsToUpdate.CollectFromPendingChanges(
            context.ChangeTracker
        );

        if (releasesAndUploadsToUpdate.IsEmpty)
        {
            return result;
        }

        var startedTransaction = context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var pendingUpdate = new PendingUpdate(
            releasesAndUploadsToUpdate,
            startedTransaction,
            context.Database.AutoSavepointsEnabled
        );
        pendingUpdatesByContext.AddOrUpdate(context, pendingUpdate);

        if (startedTransaction is not null)
        {
            context.Database.AutoSavepointsEnabled = false;
        }

        try
        {
            await releasesAndUploadsToUpdate.AddRowsChangedByDatabaseCascadeDeletesAsync(
                context,
                cancellationToken
            );
        }
        catch
        {
            await RemovePendingUpdateAndRollBackStartedTransactionAsync(context);
            throw;
        }

        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default
    )
    {
        var context = (BearcatDbContext)eventData.Context!;
        if (!pendingUpdatesByContext.TryGetValue(context, out var pendingUpdate))
        {
            return result;
        }

        pendingUpdatesByContext.Remove(context);
        try
        {
            await MaterializedValuesUpdater.UpdateAsync(
                context,
                pendingUpdate.ReleasesAndUploadsToUpdate,
                cancellationToken
            );

            if (pendingUpdate.StartedTransaction is not null)
            {
                await pendingUpdate.StartedTransaction.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            await DisposeStartedTransactionAndRestoreAutoSavepointsAsync(context, pendingUpdate);
        }

        return result;
    }

    public override async Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default
    )
    {
        await RemovePendingUpdateAndRollBackStartedTransactionAsync(eventData.Context!);
    }

    public override async ValueTask<InterceptionResult> ThrowingConcurrencyExceptionAsync(
        ConcurrencyExceptionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default
    )
    {
        await RemovePendingUpdateAndRollBackStartedTransactionAsync(eventData.Context!);

        return result;
    }

    public override async Task SaveChangesCanceledAsync(
        DbContextEventData eventData,
        CancellationToken cancellationToken = default
    )
    {
        await RemovePendingUpdateAndRollBackStartedTransactionAsync(eventData.Context!);
    }

    private async Task RemovePendingUpdateAndRollBackStartedTransactionAsync(DbContext context)
    {
        if (!pendingUpdatesByContext.TryGetValue(context, out var pendingUpdate))
        {
            return;
        }

        pendingUpdatesByContext.Remove(context);
        await DisposeStartedTransactionAndRestoreAutoSavepointsAsync(context, pendingUpdate);
    }

    private static async Task DisposeStartedTransactionAndRestoreAutoSavepointsAsync(
        DbContext context,
        PendingUpdate pendingUpdate
    )
    {
        if (pendingUpdate.StartedTransaction is null)
        {
            return;
        }

        await pendingUpdate.StartedTransaction.DisposeAsync();
        context.Database.AutoSavepointsEnabled = pendingUpdate.AutoSavepointsEnabledBeforeSave;
    }

    private sealed record PendingUpdate(
        ReleasesAndUploadsToUpdate ReleasesAndUploadsToUpdate,
        IDbContextTransaction? StartedTransaction,
        bool AutoSavepointsEnabledBeforeSave
    );
}
