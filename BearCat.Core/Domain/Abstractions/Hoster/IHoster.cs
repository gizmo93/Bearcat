using BearCat.Core.Domain.Abstractions.Hoster.Results;
using BearCat.Core.Domain.Entities;

namespace BearCat.Core.Domain.Abstractions.Hoster;

public interface IHoster
{
    string Name { get; }
    
    Task PrepareForUploadAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken);

    Task<UploadFileResult> UploadFileAsync(
        ArchiveFile archiveFile,
        CancellationToken cancellationToken);

    Task<FileExistResult> CheckFilesExistAsync(
        IHosterConfig hosterConfig,
        IReadOnlyList<string> fileUrls,
        CancellationToken cancellationToken);

    IHosterConfig DeserializeHosterConfig(string serializedConfig);

    string SerializeHosterConfig(IHosterConfig hosterConfig);

    Task<int?> GetMaximumParallelUploadsAsync(IHosterConfig hosterConfig, CancellationToken cancellationToken);

    Task<TryLoginResult> TryLoginAsync(IHosterConfig hosterConfig, CancellationToken cancellationToken);
}
