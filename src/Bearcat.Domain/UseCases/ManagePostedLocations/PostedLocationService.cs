using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManagePostedLocations.Repositories;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManagePostedLocations;

public class PostedLocationService(
    IPostedLocationWriteRepository writeRepository,
    TimeProvider timeProvider
)
{
    public Task<int> AddForReleaseAsync(
        int releaseId,
        string url,
        CancellationToken cancellationToken = default
    )
    {
        return AddAsync(
            postedLocation: new PostedLocation { ReleaseId = releaseId },
            url: url,
            cancellationToken: cancellationToken
        );
    }

    public Task<int> AddForCollectionAsync(
        int releaseCollectionId,
        string url,
        CancellationToken cancellationToken = default
    )
    {
        return AddAsync(
            postedLocation: new PostedLocation { ReleaseCollectionId = releaseCollectionId },
            url: url,
            cancellationToken: cancellationToken
        );
    }

    public async Task DeleteAsync(
        int postedLocationId,
        CancellationToken cancellationToken = default
    )
    {
        var postedLocation = await writeRepository.GetByIdAsync(
            postedLocationId: postedLocationId,
            cancellationToken: cancellationToken
        );

        writeRepository.Remove(postedLocation);
        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> AddAsync(
        PostedLocation postedLocation,
        string url,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("A posted location requires a URL.", nameof(url));
        }

        postedLocation.Url = url.Trim();
        postedLocation.CreatedAt = timeProvider.GetLocalNow();

        writeRepository.Add(postedLocation);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return postedLocation.Id;
    }
}
