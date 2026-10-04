using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Website.Pages.ManageReleaseCollections.Images;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.ManageReleaseCollections.Images;

public class CollectionImageUploadPreviewUrlsTest
{
    [Test]
    public void GetThumbnailUrl_AllSizesExist_ReturnsThumbnailUrl()
    {
        // Arrange
        var imageUrls = CreateImageUrls(ImageSize.Full, ImageSize.Medium, ImageSize.Thumbnail);

        // Act
        var url = CollectionImageUploadPreviewUrls.GetThumbnailUrl(imageUrls);

        // Assert
        url.ShouldBe("https://images.example/thumbnail.jpg");
    }

    [Test]
    public void GetThumbnailUrl_NoThumbnail_ReturnsMediumUrl()
    {
        // Arrange
        var imageUrls = CreateImageUrls(ImageSize.Full, ImageSize.Medium);

        // Act
        var url = CollectionImageUploadPreviewUrls.GetThumbnailUrl(imageUrls);

        // Assert
        url.ShouldBe("https://images.example/medium.jpg");
    }

    [Test]
    public void GetThumbnailUrl_OnlyFull_ReturnsFullUrl()
    {
        // Arrange
        var imageUrls = CreateImageUrls(ImageSize.Full);

        // Act
        var url = CollectionImageUploadPreviewUrls.GetThumbnailUrl(imageUrls);

        // Assert
        url.ShouldBe("https://images.example/full.jpg");
    }

    [Test]
    public void GetThumbnailUrl_NoUrls_ReturnsNull()
    {
        // Act
        var url = CollectionImageUploadPreviewUrls.GetThumbnailUrl([]);

        // Assert
        url.ShouldBeNull();
    }

    [Test]
    public void GetFullSizeUrl_AllSizesExist_ReturnsFullUrl()
    {
        // Arrange
        var imageUrls = CreateImageUrls(ImageSize.Thumbnail, ImageSize.Medium, ImageSize.Full);

        // Act
        var url = CollectionImageUploadPreviewUrls.GetFullSizeUrl(imageUrls);

        // Assert
        url.ShouldBe("https://images.example/full.jpg");
    }

    [Test]
    public void GetFullSizeUrl_NoFull_ReturnsMediumUrl()
    {
        // Arrange
        var imageUrls = CreateImageUrls(ImageSize.Thumbnail, ImageSize.Medium);

        // Act
        var url = CollectionImageUploadPreviewUrls.GetFullSizeUrl(imageUrls);

        // Assert
        url.ShouldBe("https://images.example/medium.jpg");
    }

    [Test]
    public void GetFullSizeUrl_OnlyThumbnail_ReturnsThumbnailUrl()
    {
        // Arrange
        var imageUrls = CreateImageUrls(ImageSize.Thumbnail);

        // Act
        var url = CollectionImageUploadPreviewUrls.GetFullSizeUrl(imageUrls);

        // Assert
        url.ShouldBe("https://images.example/thumbnail.jpg");
    }

    private static List<CollectionImageUploadUrlReadModel> CreateImageUrls(
        params ImageSize[] imageSizes
    ) =>
        imageSizes
            .Select(imageSize => new CollectionImageUploadUrlReadModel(
                imageSize,
                $"https://images.example/{imageSize.ToString().ToLowerInvariant()}.jpg"
            ))
            .ToList();
}
