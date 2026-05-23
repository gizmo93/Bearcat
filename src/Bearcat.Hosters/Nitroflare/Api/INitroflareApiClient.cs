using Bearcat.Hosters.Nitroflare.Api.File;

namespace Bearcat.Hosters.Nitroflare.Api;

public interface INitroflareApiClient
{
    Task<UploadFileResponse> UploadFileAsync(
        NitroflareConfig config,
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken
    );

    Task<UploadFileResponse> TestUserHashAsync(
        NitroflareConfig config,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyDictionary<string, bool>> CheckLinksAsync(
        IReadOnlyList<string> fileUrls,
        CancellationToken cancellationToken
    );
}
