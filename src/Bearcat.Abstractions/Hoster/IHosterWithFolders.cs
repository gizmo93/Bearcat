namespace Bearcat.Abstractions.Hoster;

public interface IHosterWithFolders : IHoster
{
    Task<string> CreateFolderAsync(
        string folderName,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    );
}
