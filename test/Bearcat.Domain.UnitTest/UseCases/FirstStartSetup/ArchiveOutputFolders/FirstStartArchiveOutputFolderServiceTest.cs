using Bearcat.Abstractions;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.FirstStartSetup.ArchiveOutputFolders;
using Bearcat.Domain.UseCases.FirstStartSetup.Exceptions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.FirstStartSetup.ArchiveOutputFolders;

public class FirstStartArchiveOutputFolderServiceTest
{
    private static readonly string WorkingDirectoryPath = Path.Combine(
        Path.GetTempPath(),
        "bearcat-working"
    );

    private Mock<IFileSystemService> fileSystemServiceMock = null!;
    private FirstStartArchiveOutputFolderService service = null!;

    [SetUp]
    public void Setup()
    {
        fileSystemServiceMock = new Mock<IFileSystemService>();
        fileSystemServiceMock
            .Setup(fileSystemService => fileSystemService.DirectoryExists(WorkingDirectoryPath))
            .Returns(true);
        service = new FirstStartArchiveOutputFolderService(
            fileSystemServiceMock.Object,
            Options.Create(
                new WorkingDirectoriesConfig { WorkingDirectories = [WorkingDirectoryPath] }
            )
        );
    }

    [Test]
    public void CreateArchiveOutputFolder_FolderInsideWorkingDirectory_CreatesFolder()
    {
        // Arrange
        var folderPath = Path.Combine(WorkingDirectoryPath, "bearcat-archives");

        // Act
        service.CreateArchiveOutputFolder(folderPath);

        // Assert
        fileSystemServiceMock.Verify(
            fileSystemService => fileSystemService.CreateDirectory(folderPath),
            Times.Once
        );
    }

    [TestCase("")]
    [TestCase("..")]
    [TestCase("../bearcat-archives")]
    [TestCase("../bearcat-working-other/bearcat-archives")]
    public void CreateArchiveOutputFolder_FolderNotStrictlyInsideWorkingDirectory_Throws(
        string relativeFolderPath
    )
    {
        // Arrange
        var folderPath = Path.Combine(WorkingDirectoryPath, relativeFolderPath);

        // Act
        var action = () => service.CreateArchiveOutputFolder(folderPath);

        // Assert
        action.ShouldThrow<ArchiveOutputFolderNotInsideExistingWorkingDirectoryException>();
        fileSystemServiceMock.Verify(
            fileSystemService => fileSystemService.CreateDirectory(It.IsAny<string>()),
            Times.Never
        );
    }
}
