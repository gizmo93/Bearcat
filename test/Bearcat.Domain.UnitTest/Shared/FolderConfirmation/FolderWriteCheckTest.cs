using Bearcat.Abstractions;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.FolderConfirmation;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared.FolderConfirmation;

public class FolderWriteCheckTest
{
    private Mock<IConfirmedFolderRepository> repositoryMock = null!;
    private Mock<INotificationService> notificationServiceMock = null!;
    private FolderWriteCheck folderWriteCheck = null!;

    [SetUp]
    public void SetUp()
    {
        repositoryMock = new Mock<IConfirmedFolderRepository>();
        notificationServiceMock = new Mock<INotificationService>();
        folderWriteCheck = new FolderWriteCheck(
            new FolderConfirmationCheck(
                new ConfirmableFolderRootProvider(
                    Options.Create(
                        new WorkingDirectoriesConfig { WorkingDirectories = ["/mnt/data"] }
                    )
                ),
                repositoryMock.Object,
                Mock.Of<IFileSystemService>()
            ),
            repositoryMock.Object,
            notificationServiceMock.Object,
            NullLogger<FolderWriteCheck>.Instance
        );
    }

    [Test]
    public async Task IsWriteAllowedOtherwiseNotifyAsync_PathsOutsideWorkingDirectoriesOrEmpty_ReturnsTrueWithoutNotification()
    {
        // Act
        var result = await folderWriteCheck.IsWriteAllowedOtherwiseNotifyAsync(
            ["/srv/archives", null, " "],
            default
        );

        // Assert
        result.ShouldBeTrue();
        notificationServiceMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task IsWriteAllowedOtherwiseNotifyAsync_PathInNotConfirmedWorkingDirectory_ReturnsFalseAndNotifies()
    {
        // Act
        var result = await folderWriteCheck.IsWriteAllowedOtherwiseNotifyAsync(
            ["/srv/archives", "/mnt/data/release"],
            default
        );

        // Assert
        result.ShouldBeFalse();
        notificationServiceMock.Verify(
            notificationService =>
                notificationService.CreateAsync(
                    NotificationKind.FolderNotConfirmed,
                    FolderNotConfirmedNotificationMessage.Get(
                        new FolderConfirmationResult(
                            FolderConfirmationState.NotConfirmed,
                            "/mnt/data"
                        )
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task IsWriteAllowedOtherwiseNotifyAsync_UnresolvedNotificationExists_DoesNotNotifyAgain()
    {
        // Arrange
        repositoryMock
            .Setup(repository =>
                repository.AnyUnresolvedFolderNotConfirmedNotificationAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true);

        // Act
        var result = await folderWriteCheck.IsWriteAllowedOtherwiseNotifyAsync(
            "/mnt/data/release",
            default
        );

        // Assert
        result.ShouldBeFalse();
        notificationServiceMock.VerifyNoOtherCalls();
    }
}
