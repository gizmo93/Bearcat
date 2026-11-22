using Bearcat.Abstractions.ImageHoster.Results;

namespace Bearcat.Abstractions.ImageHoster;

/// <summary>
/// Checks whether an uploaded image still exists on the image hoster. Returns true or false only
/// for a recognized online or offline response and throws for every other response.
/// </summary>
public interface ISupportsImageOnlineCheck
{
    Task<bool> CheckImageExistsAsync(
        IReadOnlyList<ImageUrl> imageUrls,
        IImageHosterConfig imageHosterConfig,
        CancellationToken cancellationToken = default
    );
}
