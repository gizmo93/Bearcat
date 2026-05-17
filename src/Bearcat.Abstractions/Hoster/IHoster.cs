using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Results;

namespace Bearcat.Abstractions.Hoster;

public interface IHoster
{
    string Name { get; }

    Task<UploadFileResult> UploadFileAsync(
        FileDto fileDto,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    );

    Task<FileExistResult> CheckFilesExistAsync(
        IHosterConfig hosterConfig,
        IReadOnlyList<string> fileUrls,
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
