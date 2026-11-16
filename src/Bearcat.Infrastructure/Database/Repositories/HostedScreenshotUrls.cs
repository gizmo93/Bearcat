using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Infrastructure.Database.Repositories;

internal static class HostedScreenshotUrls
{
    public static string? SelectFullUrl(IReadOnlyDictionary<ImageSize, string> urlsBySize)
    {
        return urlsBySize.GetValueOrDefault(ImageSize.Full)
            ?? urlsBySize.GetValueOrDefault(ImageSize.Medium)
            ?? urlsBySize.GetValueOrDefault(ImageSize.Thumbnail);
    }

    public static string? SelectPreviewUrl(
        ReleaseScreenshotKind kind,
        IReadOnlyDictionary<ImageSize, string> urlsBySize
    )
    {
        if (kind is ReleaseScreenshotKind.ScreenshotGrid)
        {
            return SelectFullUrl(urlsBySize);
        }

        return urlsBySize.GetValueOrDefault(ImageSize.Medium) ?? SelectFullUrl(urlsBySize);
    }
}
