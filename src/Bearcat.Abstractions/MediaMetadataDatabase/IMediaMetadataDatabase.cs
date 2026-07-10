namespace Bearcat.Abstractions.MediaMetadataDatabase;

public interface IMediaMetadataDatabase
{
    string Name { get; }

    int ResolutionPriority { get; }

    IReadOnlyList<MediaKind> SupportedMediaKinds { get; }

    IReadOnlyList<string> ConfigurationKeys { get; }

    Task<MediaMetadata?> GetByImdbIdAsync(
        IMediaMetadataDatabaseConfig config,
        MediaMetadataLookup lookup,
        CancellationToken cancellationToken = default
    );

    Task<MediaMetadata?> GetByTitleAsync(
        IMediaMetadataDatabaseConfig config,
        MediaMetadataLookup lookup,
        CancellationToken cancellationToken = default
    );

    Task<TryLoginResult> TryLoginAsync(
        IMediaMetadataDatabaseConfig config,
        CancellationToken cancellationToken = default
    );

    string SerializeConfig(IReadOnlyDictionary<string, string> config);

    IMediaMetadataDatabaseConfig DeserializeConfig(string serializedConfig);
}
