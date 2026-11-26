using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageUploads.EmptyFolderCleanup;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Bearcat.Domain.UnitTest.UseCases.ManageUploads.EmptyFolderCleanup;

public class EmptyHosterFolderCleanupServiceTest
{
    private const string HosterClassName = "FolderHoster";

    private HosterRegistration registration = null!;
    private Mock<IEmptyHosterFolderCleanupRepository> repositoryMock = null!;
    private Mock<IHosterWithEmptyFolderCleanup> hosterMock = null!;
    private Mock<IHosterFactory> hosterFactoryMock = null!;

    [SetUp]
    public void SetUp()
    {
        registration = new HosterRegistration
        {
            Id = 7,
            Name = "Folder hoster account",
            SerializedConfig = "protected-config",
            HosterClassName = HosterClassName,
            IsActive = true,
        };

        repositoryMock = new Mock<IEmptyHosterFolderCleanupRepository>();

        repositoryMock
            .Setup(x =>
                x.GetHosterRegistrationsWithEmptyFolderDeletionAsync(It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([registration]);

        hosterMock = new Mock<IHosterWithEmptyFolderCleanup>();

        hosterMock
            .Setup(x => x.DeserializeHosterConfig(It.IsAny<string>()))
            .Returns(Mock.Of<IHosterConfig>());

        hosterFactoryMock = new Mock<IHosterFactory>();
        hosterFactoryMock.Setup(x => x.GetByName(HosterClassName)).Returns(hosterMock.Object);
    }

    [Test]
    public async Task DeleteEmptyFoldersAsync_FolderIsEmpty_DeletesFolderAndClearsFolderId()
    {
        // Arrange
        SetUpFolderContentStates(
            new Dictionary<string, HosterFolderContentState>
            {
                ["empty"] = HosterFolderContentState.Empty,
            }
        );

        // Act
        await CreateService().DeleteEmptyFoldersAsync(CancellationToken.None);

        // Assert
        hosterMock.Verify(
            x =>
                x.DeleteFolderAsync(
                    "empty",
                    It.IsAny<IHosterConfig>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        repositoryMock.Verify(
            x => x.ClearFolderIdAsync(registration.Id, "empty", It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Test]
    public async Task DeleteEmptyFoldersAsync_FolderNotFound_ClearsFolderIdWithoutDeleting()
    {
        // Arrange
        SetUpFolderContentStates(
            new Dictionary<string, HosterFolderContentState>
            {
                ["missing"] = HosterFolderContentState.NotFound,
            }
        );

        // Act
        await CreateService().DeleteEmptyFoldersAsync(CancellationToken.None);

        // Assert
        hosterMock.Verify(
            x =>
                x.DeleteFolderAsync(
                    It.IsAny<string>(),
                    It.IsAny<IHosterConfig>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );

        repositoryMock.Verify(
            x => x.ClearFolderIdAsync(registration.Id, "missing", It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Test]
    public async Task DeleteEmptyFoldersAsync_FolderNotEmpty_NeitherDeletesNorClearsFolderId()
    {
        // Arrange
        SetUpFolderContentStates(
            new Dictionary<string, HosterFolderContentState>
            {
                ["filled"] = HosterFolderContentState.NotEmpty,
            }
        );

        // Act
        await CreateService().DeleteEmptyFoldersAsync(CancellationToken.None);

        // Assert
        hosterMock.Verify(
            x =>
                x.DeleteFolderAsync(
                    It.IsAny<string>(),
                    It.IsAny<IHosterConfig>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );

        repositoryMock.Verify(
            x =>
                x.ClearFolderIdAsync(
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task DeleteEmptyFoldersAsync_DeleteFails_KeepsFolderIdAndDeletesNextFolder()
    {
        // Arrange
        SetUpFolderContentStates(
            new Dictionary<string, HosterFolderContentState>
            {
                ["failing"] = HosterFolderContentState.Empty,
                ["empty"] = HosterFolderContentState.Empty,
            }
        );

        hosterMock
            .Setup(x =>
                x.DeleteFolderAsync(
                    "failing",
                    It.IsAny<IHosterConfig>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new HttpRequestException("Error: Folder not found"));

        // Act
        await CreateService().DeleteEmptyFoldersAsync(CancellationToken.None);

        // Assert
        repositoryMock.Verify(
            x => x.ClearFolderIdAsync(registration.Id, "failing", It.IsAny<CancellationToken>()),
            Times.Never
        );

        repositoryMock.Verify(
            x => x.ClearFolderIdAsync(registration.Id, "empty", It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Test]
    public async Task DeleteEmptyFoldersAsync_ThreeConsecutiveDeletesFail_StopsDeletingOnRegistration()
    {
        // Arrange
        SetUpFolderContentStates(
            new Dictionary<string, HosterFolderContentState>
            {
                ["first"] = HosterFolderContentState.Empty,
                ["second"] = HosterFolderContentState.Empty,
                ["third"] = HosterFolderContentState.Empty,
                ["fourth"] = HosterFolderContentState.Empty,
                ["fifth"] = HosterFolderContentState.Empty,
            }
        );

        hosterMock
            .Setup(x =>
                x.DeleteFolderAsync(
                    It.IsAny<string>(),
                    It.IsAny<IHosterConfig>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new HttpRequestException("Error: Folder not found"));

        // Act
        await CreateService().DeleteEmptyFoldersAsync(CancellationToken.None);

        // Assert
        hosterMock.Verify(
            x =>
                x.DeleteFolderAsync(
                    It.IsAny<string>(),
                    It.IsAny<IHosterConfig>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(3)
        );
    }

    [Test]
    public async Task DeleteEmptyFoldersAsync_HosterWithoutEmptyFolderCleanup_SkipsRegistration()
    {
        // Arrange
        hosterFactoryMock.Setup(x => x.GetByName(HosterClassName)).Returns(Mock.Of<IHoster>());

        // Act
        await CreateService().DeleteEmptyFoldersAsync(CancellationToken.None);

        // Assert
        repositoryMock.Verify(
            x =>
                x.GetFolderIdsWithoutOnlineFilesAsync(
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    private void SetUpFolderContentStates(
        Dictionary<string, HosterFolderContentState> statesByFolderId
    )
    {
        repositoryMock
            .Setup(x =>
                x.GetFolderIdsWithoutOnlineFilesAsync(
                    registration.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(statesByFolderId.Keys.ToList());

        hosterMock
            .Setup(x =>
                x.GetFolderContentStatesAsync(
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<IHosterConfig>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (IReadOnlyList<string> folderIds, IHosterConfig _, CancellationToken _) =>
                    folderIds
                        .Select(folderId => new HosterFolderContentStateDto(
                            folderId,
                            statesByFolderId[folderId]
                        ))
                        .ToList()
            );
    }

    private EmptyHosterFolderCleanupService CreateService()
    {
        var secretProtectorMock = new Mock<ISecretProtector>();
        secretProtectorMock.Setup(x => x.Unprotect(It.IsAny<string>())).Returns("{}");
        var notificationService = Mock.Of<INotificationService>();

        return new EmptyHosterFolderCleanupService(
            repository: repositoryMock.Object,
            hosterFactory: hosterFactoryMock.Object,
            secretProtector: secretProtectorMock.Object,
            captchaVerificationService: new HosterCaptchaVerificationService(notificationService),
            credentialsRejectionService: new HosterCredentialsRejectionService(notificationService),
            logger: NullLogger<EmptyHosterFolderCleanupService>.Instance
        );
    }
}
