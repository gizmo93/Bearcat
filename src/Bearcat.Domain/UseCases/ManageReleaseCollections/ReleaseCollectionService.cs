using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections;

public class ReleaseCollectionService(
    IReleaseCollectionWriteRepository writeRepository,
    TimeProvider timeProvider
)
{
    public async Task<int> CreateAsync(
        string name,
        string key,
        int releaseGroupId,
        CancellationToken cancellationToken = default
    )
    {
        var releaseCollection = new ReleaseCollection
        {
            Name = CleanRequired(name, nameof(name)),
            Key = CleanRequired(key, nameof(key)),
            ReleaseGroupId = releaseGroupId,
            CreatedAt = timeProvider.GetLocalNow(),
        };

        writeRepository.Add(releaseCollection);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return releaseCollection.Id;
    }

    public async Task UpdateAsync(
        int releaseCollectionId,
        string name,
        CancellationToken cancellationToken = default
    )
    {
        var releaseCollection = await writeRepository.GetByIdAsync(
            releaseCollectionId,
            cancellationToken
        );
        releaseCollection.Name = CleanRequired(name, nameof(name));

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    )
    {
        var releaseCollection = await writeRepository.GetByIdAsync(
            releaseCollectionId,
            cancellationToken
        );
        writeRepository.Remove(releaseCollection);

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    private static string CleanRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return value.Trim();
    }
}
