using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;

namespace Bearcat.Website.Pages.ManageReleaseCollections.Images;

public static class CollectionImageUploadPreviewUrls
{
    private static readonly IReadOnlyList<ImageSize> ThumbnailImageSizePreference =
    [
        ImageSize.Thumbnail,
        ImageSize.Medium,
        ImageSize.Full,
    ];

    private static readonly IReadOnlyList<ImageSize> FullSizeImageSizePreference =
    [
        ImageSize.Full,
        ImageSize.Medium,
        ImageSize.Thumbnail,
    ];

    public static string? GetThumbnailUrl(
        IReadOnlyList<CollectionImageUploadUrlReadModel> imageUrls
    ) => GetFirstUrlBySizePreference(imageUrls, ThumbnailImageSizePreference);

    public static string? GetFullSizeUrl(
        IReadOnlyList<CollectionImageUploadUrlReadModel> imageUrls
    ) => GetFirstUrlBySizePreference(imageUrls, FullSizeImageSizePreference);

    private static string? GetFirstUrlBySizePreference(
        IReadOnlyList<CollectionImageUploadUrlReadModel> imageUrls,
        IReadOnlyList<ImageSize> imageSizePreference
    ) =>
        imageSizePreference
            .Select(imageSize =>
                imageUrls.FirstOrDefault(imageUrl => imageUrl.ImageSize == imageSize)?.Url
            )
            .FirstOrDefault(url => url is not null);
}
