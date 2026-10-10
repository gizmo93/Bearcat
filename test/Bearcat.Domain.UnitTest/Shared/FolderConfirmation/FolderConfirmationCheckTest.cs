using Bearcat.Abstractions;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.FolderConfirmation;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared.FolderConfirmation;

public class FolderConfirmationCheckTest
{
    private static readonly Guid MarkerId = Guid.Parse("4f7d7f0e-5d55-4d8e-9a43-7d5f9b4a3e21");

    private Mock<IConfirmedFolderRepository> repositoryMock = null!;
    private Mock<IFileSystemService> fileSystemServiceMock = null!;
    private WorkingDirectoriesConfig workingDirectoriesConfig = null!;
    private FolderConfirmationCheck check = null!;

    [SetUp]
    public void SetUp()
    {
        repositoryMock = new Mock<IConfirmedFolderRepository>();
        fileSystemServiceMock = new Mock<IFileSystemService>();
        workingDirectoriesConfig = new WorkingDirectoriesConfig();
        check = new FolderConfirmationCheck(
            new ConfirmableFolderRootProvider(Options.Create(workingDirectoriesConfig)),
            repositoryMock.Object,
            fileSystemServiceMock.Object
        );
    }

    [Test]
    public async Task GetFolderConfirmationAsync_NoWorkingDirectoryConfigured_ReturnsOutsideConfirmableFolders()
    {
        // Act
        var result = await check.GetFolderConfirmationAsync("/mnt/data/release", default);

        // Assert
        result.ShouldBe(
            new FolderConfirmationResult(FolderConfirmationState.OutsideConfirmableFolders, null)
        );
        result.IsWriteAllowed.ShouldBeTrue();
    }

    [Test]
    public async Task GetFolderConfirmationAsync_BlankWorkingDirectories_AreIgnored()
    {
        // Arrange
        workingDirectoriesConfig.WorkingDirectories = ["", "   "];

        // Act
        var result = await check.GetFolderConfirmationAsync("/mnt/data/release", default);

        // Assert
        result.State.ShouldBe(FolderConfirmationState.OutsideConfirmableFolders);
        repositoryMock.Verify(
            repository => repository.GetMarkerIdAsync(It.IsAny<string>(), default),
            Times.Never
        );
    }

    [Test]
    public async Task GetFolderConfirmationAsync_PathOutsideWorkingDirectories_ReturnsOutsideConfirmableFolders()
    {
        // Arrange
        workingDirectoriesConfig.WorkingDirectories = ["/mnt/data"];

        // Act
        var result = await check.GetFolderConfirmationAsync("/mnt/database/release", default);

        // Assert
        result.State.ShouldBe(FolderConfirmationState.OutsideConfirmableFolders);
        result.RootPath.ShouldBeNull();
    }

    [Test]
    public async Task GetFolderConfirmationAsync_WorkingDirectoryWithoutStoredConfirmation_ReturnsNotConfirmed()
    {
        // Arrange
        workingDirectoriesConfig.WorkingDirectories = ["/mnt/data"];

        // Act
        var result = await check.GetFolderConfirmationAsync("/mnt/data/release", default);

        // Assert
        result.ShouldBe(
            new FolderConfirmationResult(FolderConfirmationState.NotConfirmed, "/mnt/data")
        );
        result.IsWriteAllowed.ShouldBeFalse();
    }

    [Test]
    public async Task GetFolderConfirmationAsync_MarkerFileMatchesStoredMarker_ReturnsConfirmed()
    {
        // Arrange
        workingDirectoriesConfig.WorkingDirectories = ["/mnt/data"];
        SetupStoredMarker("/mnt/data");
        SetupMarkerFileContent("/mnt/data", $"{MarkerId}\n");

        // Act
        var result = await check.GetFolderConfirmationAsync("/mnt/data/release/archives", default);

        // Assert
        result.ShouldBe(
            new FolderConfirmationResult(FolderConfirmationState.Confirmed, "/mnt/data")
        );
        result.IsWriteAllowed.ShouldBeTrue();
    }

    [Test]
    public async Task GetFolderConfirmationAsync_PathIsWorkingDirectoryItself_UsesThatWorkingDirectory()
    {
        // Arrange
        workingDirectoriesConfig.WorkingDirectories = ["/mnt/data/"];
        SetupStoredMarker("/mnt/data");
        SetupMarkerFileContent("/mnt/data", MarkerId.ToString());

        // Act
        var result = await check.GetFolderConfirmationAsync("/mnt/data", default);

        // Assert
        result.ShouldBe(
            new FolderConfirmationResult(FolderConfirmationState.Confirmed, "/mnt/data")
        );
    }

    [Test]
    public async Task GetFolderConfirmationAsync_MarkerFileMissing_ReturnsMarkerMissing()
    {
        // Arrange
        workingDirectoriesConfig.WorkingDirectories = ["/mnt/data"];
        SetupStoredMarker("/mnt/data");

        // Act
        var result = await check.GetFolderConfirmationAsync("/mnt/data/release", default);

        // Assert
        result.ShouldBe(
            new FolderConfirmationResult(FolderConfirmationState.MarkerMissing, "/mnt/data")
        );
        result.IsWriteAllowed.ShouldBeFalse();
    }

    [TestCase("")]
    [TestCase("not a guid")]
    [TestCase("0b1e1c55-91a4-4cd2-8f43-5b3c6a0f7e10")]
    public async Task GetFolderConfirmationAsync_MarkerFileContentDiffers_ReturnsMarkerMissing(
        string markerFileContent
    )
    {
        // Arrange
        workingDirectoriesConfig.WorkingDirectories = ["/mnt/data"];
        SetupStoredMarker("/mnt/data");
        SetupMarkerFileContent("/mnt/data", markerFileContent);

        // Act
        var result = await check.GetFolderConfirmationAsync("/mnt/data/release", default);

        // Assert
        result.State.ShouldBe(FolderConfirmationState.MarkerMissing);
    }

    [Test]
    public async Task GetFolderConfirmationAsync_NestedWorkingDirectories_UsesLongestMatchingWorkingDirectory()
    {
        // Arrange
        workingDirectoriesConfig.WorkingDirectories = ["/mnt/data", "/mnt/data/nas"];
        SetupStoredMarker("/mnt/data");
        SetupMarkerFileContent("/mnt/data", MarkerId.ToString());

        // Act
        var nestedResult = await check.GetFolderConfirmationAsync("/mnt/data/nas/release", default);
        var outerResult = await check.GetFolderConfirmationAsync(
            "/mnt/data/local/release",
            default
        );

        // Assert
        nestedResult.ShouldBe(
            new FolderConfirmationResult(FolderConfirmationState.NotConfirmed, "/mnt/data/nas")
        );
        outerResult.ShouldBe(
            new FolderConfirmationResult(FolderConfirmationState.Confirmed, "/mnt/data")
        );
    }

    [Test]
    public async Task GetFolderConfirmationAsync_LegacyReleaseDataDirectory_IsUsedAsWorkingDirectory()
    {
        // Arrange
        workingDirectoriesConfig.ReleaseDataDirectory = "/mnt/legacy";

        // Act
        var result = await check.GetFolderConfirmationAsync("/mnt/legacy/release", default);

        // Assert
        result.ShouldBe(
            new FolderConfirmationResult(FolderConfirmationState.NotConfirmed, "/mnt/legacy")
        );
    }

    private void SetupStoredMarker(string rootPath)
    {
        repositoryMock
            .Setup(repository =>
                repository.GetMarkerIdAsync(rootPath, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(MarkerId);
    }

    private void SetupMarkerFileContent(string rootPath, string content)
    {
        fileSystemServiceMock
            .Setup(fileSystemService =>
                fileSystemService.ReadFileTextIfExists(
                    FolderConfirmationMarkerFile.GetFilePath(rootPath)
                )
            )
            .Returns(content);
    }
}
