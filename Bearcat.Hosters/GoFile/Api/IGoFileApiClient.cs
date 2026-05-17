using Bearcat.Hosters.GoFile.Api.GetAccountId;

namespace Bearcat.Hosters.GoFile.Api;

public interface IGoFileApiClient
{
    Task<Response> GetAccountAsync(string apiKey, CancellationToken cancellationToken = default);

    Task<UploadFile.Response> UploadFileAsync(
        string apiKey,
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyDictionary<string, (bool IsOnline, string? ErrorMessage)>> CheckOnlineStatusAsync(
        IReadOnlyList<string> fileUrls,
        string apiKey,
        CancellationToken cancellationToken
    );
}
