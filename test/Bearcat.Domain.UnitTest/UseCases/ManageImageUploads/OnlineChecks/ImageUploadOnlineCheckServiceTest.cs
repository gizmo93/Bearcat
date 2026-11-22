using Bearcat.Abstractions.ImageHoster;
using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageImageUploads.OnlineChecks;
using Bearcat.Domain.UseCases.ManageImageUploads.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UnitTest.UseCases.ManageImageUploads.OnlineChecks;

public class ImageUploadOnlineCheckServiceTest
{
    private const int RegistrationId = 7;
    private const string ImageHosterClassName = "FakeImageHoster";

    private static readonly DateTime LocalNow = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Local);

    private Mock<IImageUploadRepository> repositoryMock = null!;
    private Mock<IImageHoster> imageHosterMock = null!;
    private Mock<ISupportsImageOnlineCheck> imageOnlineCheckMock = null!;
    private ImageHosterRegistration registration = null!;

    [SetUp]
    public void SetUp()
    {
        registration = new ImageHosterRegistration
        {
            Id = RegistrationId,
            Name = "Fake",
            ImageHosterClassName = ImageHosterClassName,
            SerializedConfig = "{}",
            IsActive = true,
        };

        repositoryMock = new Mock<IImageUploadRepository>();
        repositoryMock
            .Setup(x => x.GetConfigByImageHosterRegistrationIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, string> { [RegistrationId] = "{}" });

        imageHosterMock = new Mock<IImageHoster>();
        imageHosterMock
            .Setup(x => x.DeserializeConfig(It.IsAny<string>()))
            .Returns(Mock.Of<IImageHosterConfig>());
        imageOnlineCheckMock = imageHosterMock.As<ISupportsImageOnlineCheck>();
    }

    [Test]
    public async Task CheckOnlineStateOfLeastRecentlyCheckedImageUploadsAsync_ImageExists_SetsOnlineAndCheckedAt()
    {
        // Arrange
        var imageUpload = CreateImageUpload(id: 1);
        SetUpImageUploads(imageUpload);
        SetUpImageExists("https://images.test/1.jpg", imageExists: true);

        // Act
        await CreateService(imageHosterMock.Object)
            .CheckOnlineStateOfLeastRecentlyCheckedImageUploadsAsync(CancellationToken.None);

        // Assert
        imageUpload.OnlineState.ShouldBe(OnlineState.Online);
        imageUpload.OnlineCheckedAt.ShouldBe(LocalNow);
        imageUpload.OfflineSince.ShouldBeNull();
        repositoryMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CheckOnlineStateOfLeastRecentlyCheckedImageUploadsAsync_ImageDoesNotExist_SetsOfflineAndOfflineSince()
    {
        // Arrange
        var imageUpload = CreateImageUpload(id: 1);
        SetUpImageUploads(imageUpload);
        SetUpImageExists("https://images.test/1.jpg", imageExists: false);

        // Act
        await CreateService(imageHosterMock.Object)
            .CheckOnlineStateOfLeastRecentlyCheckedImageUploadsAsync(CancellationToken.None);

        // Assert
        imageUpload.OnlineState.ShouldBe(OnlineState.Offline);
        imageUpload.OnlineCheckedAt.ShouldBe(LocalNow);
        imageUpload.OfflineSince.ShouldBe(LocalNow);
    }

    [Test]
    public void SetOnlineStateAndCheckedAt_OfflineRepeatedThenOnline_KeepsFirstOfflineSinceThenClearsIt()
    {
        // Arrange
        var imageUpload = CreateImageUpload(id: 1);

        // Act
        imageUpload.SetOnlineStateAndCheckedAt(OnlineState.Offline, LocalNow.AddHours(-12));
        imageUpload.SetOnlineStateAndCheckedAt(OnlineState.Offline, LocalNow.AddHours(-6));
        var offlineSinceAfterRepeatedOffline = imageUpload.OfflineSince;
        imageUpload.SetOnlineStateAndCheckedAt(OnlineState.Online, LocalNow);

        // Assert
        offlineSinceAfterRepeatedOffline.ShouldBe(LocalNow.AddHours(-12));
        imageUpload.OnlineState.ShouldBe(OnlineState.Online);
        imageUpload.OnlineCheckedAt.ShouldBe(LocalNow);
        imageUpload.OfflineSince.ShouldBeNull();
    }

    [Test]
    public async Task CheckOnlineStateOfLeastRecentlyCheckedImageUploadsAsync_CheckThrows_KeepsStateSetsCheckedAtAndChecksNextUpload()
    {
        // Arrange
        var offlineSince = LocalNow.AddDays(-1);
        var failingImageUpload = CreateImageUpload(id: 1);
        failingImageUpload.SetOnlineStateAndCheckedAt(OnlineState.Offline, offlineSince);
        var nextImageUpload = CreateImageUpload(id: 2);
        SetUpImageUploads(failingImageUpload, nextImageUpload);
        imageOnlineCheckMock
            .Setup(x =>
                x.CheckImageExistsAsync(
                    It.Is<IReadOnlyList<ImageUrl>>(urls =>
                        urls.Single().Url == "https://images.test/1.jpg"
                    ),
                    It.IsAny<IImageHosterConfig>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new HttpRequestException("Service unavailable"));
        SetUpImageExists("https://images.test/2.jpg", imageExists: true);

        // Act
        await CreateService(imageHosterMock.Object)
            .CheckOnlineStateOfLeastRecentlyCheckedImageUploadsAsync(CancellationToken.None);

        // Assert
        failingImageUpload.OnlineState.ShouldBe(OnlineState.Offline);
        failingImageUpload.OfflineSince.ShouldBe(offlineSince);
        failingImageUpload.OnlineCheckedAt.ShouldBe(LocalNow);
        nextImageUpload.OnlineState.ShouldBe(OnlineState.Online);
        nextImageUpload.OnlineCheckedAt.ShouldBe(LocalNow);
    }

    [Test]
    public async Task CheckOnlineStateOfLeastRecentlyCheckedImageUploadsAsync_HosterWithoutOnlineCheck_LeavesUploadUnchecked()
    {
        // Arrange
        var imageUpload = CreateImageUpload(id: 1);
        SetUpImageUploads(imageUpload);
        var imageHosterWithoutOnlineCheckMock = new Mock<IImageHoster>();

        // Act
        await CreateService(imageHosterWithoutOnlineCheckMock.Object)
            .CheckOnlineStateOfLeastRecentlyCheckedImageUploadsAsync(CancellationToken.None);

        // Assert
        imageUpload.OnlineState.ShouldBe(OnlineState.Unknown);
        imageUpload.OnlineCheckedAt.ShouldBeNull();
        imageHosterWithoutOnlineCheckMock.Verify(
            x => x.DeserializeConfig(It.IsAny<string>()),
            Times.Never
        );
        repositoryMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private ImageUploadOnlineCheckService CreateService(IImageHoster imageHoster)
    {
        var imageHosterFactoryMock = new Mock<IImageHosterFactory>();
        imageHosterFactoryMock
            .Setup(x => x.GetByClassName())
            .Returns(new Dictionary<string, IImageHoster> { [ImageHosterClassName] = imageHoster });

        var timeProviderMock = new Mock<TimeProvider>(new ConfigurationBuilder().Build());
        timeProviderMock.Setup(x => x.GetLocalNow()).Returns(LocalNow);

        return new ImageUploadOnlineCheckService(
            repository: repositoryMock.Object,
            imageHosterFactory: imageHosterFactoryMock.Object,
            timeProvider: timeProviderMock.Object,
            logger: NullLogger<ImageUploadOnlineCheckService>.Instance
        );
    }

    private void SetUpImageUploads(params ImageUpload[] imageUploads)
    {
        repositoryMock
            .Setup(x =>
                x.GetLeastRecentlyCheckedImageUploadsAsync(
                    RegistrationId,
                    LocalNow - ImageUploadOnlineCheckService.MinimumCheckInterval,
                    ImageUploadOnlineCheckService.MaximumImageUploadsToCheckPerRegistrationPerRun,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(imageUploads.ToList());
    }

    private void SetUpImageExists(string url, bool imageExists)
    {
        imageOnlineCheckMock
            .Setup(x =>
                x.CheckImageExistsAsync(
                    It.Is<IReadOnlyList<ImageUrl>>(urls => urls.Single().Url == url),
                    It.IsAny<IImageHosterConfig>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(imageExists);
    }

    private ImageUpload CreateImageUpload(int id)
    {
        return new ImageUpload
        {
            Id = id,
            ImageUploadConfig = new ImageUploadConfig
            {
                Name = "Cover",
                ImageHosterRegistrationId = RegistrationId,
                ImageHosterRegistration = registration,
            },
            UploadState = UploadState.Completed,
            ImageUrls =
            [
                new ImageUploadUrl
                {
                    ImageSize = ImageSize.Full,
                    Url = $"https://images.test/{id}.jpg",
                },
            ],
        };
    }
}
