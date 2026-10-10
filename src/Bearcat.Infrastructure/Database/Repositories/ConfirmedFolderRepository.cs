using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.FolderConfirmation;
using Bearcat.Domain.UseCases.ManageConfirmedFolders.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ConfirmedFolderRepository(IBearcatReadDbContext dbRead, IBearcatWriteDbContext dbWrite)
    : IConfirmedFolderRepository,
        IConfirmedFolderWriteRepository
{
    public async Task<Guid?> GetMarkerIdAsync(string path, CancellationToken cancellationToken)
    {
        return await dbRead
            .ConfirmedFolders.Where(confirmedFolder => confirmedFolder.Path == path)
            .Select(confirmedFolder => (Guid?)confirmedFolder.MarkerId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> AnyUnresolvedFolderNotConfirmedNotificationAsync(
        string message,
        CancellationToken cancellationToken
    )
    {
        return await dbRead.Notifications.AnyAsync(
            notification =>
                notification.NotificationKind == NotificationKind.FolderNotConfirmed
                && notification.ResolvedAt == null
                && notification.Message == message,
            cancellationToken
        );
    }

    public async Task<ConfirmedFolder?> GetForUpdateAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite.ConfirmedFolders.SingleOrDefaultAsync(
            confirmedFolder => confirmedFolder.Path == path,
            cancellationToken
        );
    }

    public void Add(ConfirmedFolder confirmedFolder)
    {
        dbWrite.Add(confirmedFolder);
    }

    public async Task ResolveFolderNotConfirmedNotificationsAsync(
        IReadOnlyList<string> messages,
        DateTime resolvedAt,
        CancellationToken cancellationToken
    )
    {
        await dbWrite
            .Notifications.Where(notification =>
                notification.NotificationKind == NotificationKind.FolderNotConfirmed
                && notification.ResolvedAt == null
                && messages.Contains(notification.Message)
            )
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(notification => notification.ResolvedAt, resolvedAt),
                cancellationToken
            );
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
