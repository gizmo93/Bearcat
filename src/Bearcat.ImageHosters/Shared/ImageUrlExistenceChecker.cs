using System.Net;
using Bearcat.Abstractions.ImageHoster.Exceptions;
using Bearcat.Abstractions.ImageHoster.Results;

namespace Bearcat.ImageHosters.Shared;

public class ImageUrlExistenceChecker(HttpClient httpClient)
{
    public Task<bool> CheckAllImageUrlsExistAsync(
        IReadOnlyList<ImageUrl> imageUrls,
        CancellationToken cancellationToken
    )
    {
        return CheckAllImageUrlsExistAsync(
            imageUrls: imageUrls,
            removedImagePlaceholderUrl: null,
            cancellationToken: cancellationToken
        );
    }

    public async Task<bool> CheckAllImageUrlsExistAsync(
        IReadOnlyList<ImageUrl> imageUrls,
        string? removedImagePlaceholderUrl,
        CancellationToken cancellationToken
    )
    {
        foreach (var imageUrl in imageUrls)
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, imageUrl.Url);

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );

            var finalUrl = response.RequestMessage!.RequestUri!.AbsoluteUri;

            if (finalUrl == removedImagePlaceholderUrl)
            {
                return false;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new ImageOnlineCheckFailedException(
                    $"Image online check for {imageUrl.Url} returned unexpected status code {(int)response.StatusCode}."
                );
            }
        }

        return true;
    }
}
