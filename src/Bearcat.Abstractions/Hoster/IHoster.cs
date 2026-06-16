using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Results;

namespace Bearcat.Abstractions.Hoster;

public interface IHoster
{
    string Name { get; }

    bool SupportsPremiumOnlyDownloads { get; }

    /// <summary>
    /// True when the maximum number of parallel uploads is authoritative (e.g. reported by the
    /// hoster API) and must not be overridden. False when the value is an assumed default that the
    /// user may override per registration.
    /// </summary>
    bool HasFixedParallelUploadLimit { get; }

    /// <summary>
    /// The assumed default number of parallel uploads for hosters where the limit may be
    /// overridden. Null when the limit is fixed (i.e. only known via the hoster API).
    /// </summary>
    int? DefaultMaximumParallelUploads { get; }

    Task<UploadFileResult> UploadFileAsync(
        FileDto fileDto,
        IHosterConfig hosterConfig,
        IUploadProgress progress,
        CancellationToken cancellationToken
    );

    Task<FileExistResult> CheckFilesExistAsync(
        IHosterConfig hosterConfig,
        IReadOnlyList<FileUrlToCheckDto> files,
        CancellationToken cancellationToken
    );

    IHosterConfig DeserializeHosterConfig(string serializedConfig);

    string SerializeHosterConfig(Dictionary<string, string> hosterConfig);

    IReadOnlyList<string> ConfigurationKeys { get; }

    Task<int?> GetMaximumParallelUploadsAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    );

    Task<TryLoginResult> TryLoginAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    );
}
