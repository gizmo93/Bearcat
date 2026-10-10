using Bearcat.Infrastructure.FileSystem;
using Shouldly;

namespace Bearcat.Infrastructure.UnitTest.FileSystem;

public class FileSystemServiceTest
{
    private string rootPath = null!;
    private FileSystemService fileSystemService = null!;

    [SetUp]
    public void SetUp()
    {
        rootPath = Directory.CreateTempSubdirectory("bearcat-file-system-service-").FullName;
        fileSystemService = new FileSystemService();
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(rootPath, recursive: true);
    }

    [Test]
    public void GetSelectableFoldersInPath_ExcludesDotFolders()
    {
        // Arrange
        var visibleFolderPath = Directory
            .CreateDirectory(Path.Combine(rootPath, "Visible"))
            .FullName;
        Directory.CreateDirectory(Path.Combine(rootPath, ".dotfolder"));

        // Act
        var folderPaths = fileSystemService.GetSelectableFoldersInPath(rootPath);

        // Assert
        folderPaths.ShouldBe([visibleFolderPath]);
    }

    [Test]
    public void GetSelectableFoldersInPath_IncludesFoldersWithHiddenAttribute()
    {
        // Arrange
        var hiddenFolder = Directory.CreateDirectory(Path.Combine(rootPath, "Volumes"));
        hiddenFolder.Attributes |= FileAttributes.Hidden;

        // Act
        var folderPaths = fileSystemService.GetSelectableFoldersInPath(rootPath);

        // Assert
        folderPaths.ShouldBe([hiddenFolder.FullName]);
    }
}
