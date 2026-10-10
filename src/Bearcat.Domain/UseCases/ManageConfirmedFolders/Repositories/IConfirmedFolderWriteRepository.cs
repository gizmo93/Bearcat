using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageConfirmedFolders.Repositories;

public interface IConfirmedFolderWriteRepository
{
    Task<ConfirmedFolder?> GetForUpdateAsync(string path, CancellationToken cancellationToken);

    void Add(ConfirmedFolder confirmedFolder);

    Task ResolveFolderNotConfirmedNotificationsAsync(
        IReadOnlyList<string> messages,
        DateTime resolvedAt,
        CancellationToken cancellationToken
    );

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
