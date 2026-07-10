namespace Bearcat.Domain.UseCases.ResolveMediaMetadata;

public interface IMediaMetadataResolverRepository
{
    Task<IReadOnlyList<MediaMetadataDatabaseRegistration>> GetActiveRegistrationsAsync(
        CancellationToken cancellationToken = default
    );
}
