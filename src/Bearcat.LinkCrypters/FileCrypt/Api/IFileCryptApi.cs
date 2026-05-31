using Refit;

namespace Bearcat.LinkCrypters.FileCrypt.Api;

public interface IFileCryptApi
{
    [Post("/api.php")]
    Task<Response> SendAsync(
        [Body] FormUrlEncodedContent request,
        CancellationToken cancellationToken = default
    );
}
