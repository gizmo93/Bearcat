using Bearcat.Abstractions.ImageHoster.Results;

namespace Bearcat.Abstractions.ImageHoster;

public interface ISupportsLogin
{
    Task<TryLoginResult> TryLoginAsync(
        IImageHosterConfig imageHosterConfig,
        CancellationToken cancellationToken = default
    );
}
