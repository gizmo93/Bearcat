namespace Bearcat.Domain.Shared.MediaMetadataResolution;

public interface IMediaMetadataResolverRepository
{
    Task<IReadOnlyList<MediaMetadataDatabaseRegistration>> GetActiveRegistrationsAsync(
        CancellationToken cancellationToken = default
    );
}
