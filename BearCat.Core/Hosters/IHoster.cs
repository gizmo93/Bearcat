using BearCat.Core.Hosters.Results;

namespace BearCat.Core.Hosters;

public interface IHoster
{
    string Name { get; }
    
    Task<UploadFileResult> UploadFileAsync(
        IHosterConfig hosterConfig,
        string fullFilePath,
        CancellationToken cancellationToken);
    
    Task<FileExistResult> CheckFilesExistAsync(
        IHosterConfig hosterConfig,
        IReadOnlyList<string> fileUrls,
        CancellationToken cancellationToken);

    IHosterConfig DeserializeHosterConfig(string serializedConfig);
    
    string SerializeHosterConfig(IHosterConfig hosterConfig);
    
    Task<int?> GetMaximumParallelUploadsAsync(IHosterConfig hosterConfig, CancellationToken cancellationToken);
}