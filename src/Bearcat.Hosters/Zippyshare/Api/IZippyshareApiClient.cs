namespace Bearcat.Hosters.Zippyshare.Api;

public interface IZippyshareApiClient
{
    Task<UploadFileResponse> UploadFileAsync(
        ZippyshareConfig config,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyDictionary<string, ZippyshareFileStatus>> GetFileStatusesAsync(
        IReadOnlyList<string> fileIds,
        CancellationToken cancellationToken
    );

    Task<UserResponse> GetUserAsync(ZippyshareConfig config, CancellationToken cancellationToken);
}
