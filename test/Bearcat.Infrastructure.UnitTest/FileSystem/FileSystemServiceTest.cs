using Bearcat.Abstractions.Transfers;
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

    [Test]
    public async Task CopyFileAsync_TargetDoesNotExist_CopiesFileAndReportsBytes()
    {
        // Arrange
        var sourceFilePath = Path.Combine(rootPath, "source.rar");
        var destinationFilePath = Path.Combine(rootPath, "destination.rar");
        await File.WriteAllBytesAsync(sourceFilePath, new byte[3000]);
        var progress = new RecordingTransferProgress();

        // Act
        await fileSystemService.CopyFileAsync(
            sourceFilePath,
            destinationFilePath,
            progress,
            CancellationToken.None
        );

        // Assert
        fileSystemService.GetFileSizeBytes(destinationFilePath).ShouldBe(3000);
        progress.TotalBytes.ShouldBe(3000);
        progress.TransferredBytes.ShouldBe(3000);
    }

    [Test]
    public async Task CopyFileAsync_TargetExists_ThrowsAndKeepsExistingFile()
    {
        // Arrange
        var sourceFilePath = Path.Combine(rootPath, "source.rar");
        var destinationFilePath = Path.Combine(rootPath, "destination.rar");
        await File.WriteAllTextAsync(sourceFilePath, "new");
        await File.WriteAllTextAsync(destinationFilePath, "existing");

        // Act
        await Should.ThrowAsync<IOException>(() =>
            fileSystemService.CopyFileAsync(
                sourceFilePath,
                destinationFilePath,
                NullTransferProgress.Instance,
                CancellationToken.None
            )
        );

        // Assert
        (await File.ReadAllTextAsync(destinationFilePath)).ShouldBe("existing");
    }

    [Test]
    public async Task CopyFileAsync_Canceled_DeletesPartialTargetFile()
    {
        // Arrange
        var sourceFilePath = Path.Combine(rootPath, "source.rar");
        var destinationFilePath = Path.Combine(rootPath, "destination.rar");
        await File.WriteAllTextAsync(sourceFilePath, "data");

        // Act
        await Should.ThrowAsync<OperationCanceledException>(() =>
            fileSystemService.CopyFileAsync(
                sourceFilePath,
                destinationFilePath,
                NullTransferProgress.Instance,
                new CancellationToken(canceled: true)
            )
        );

        // Assert
        File.Exists(destinationFilePath).ShouldBeFalse();
    }

    [Test]
    public void GetAvailableFreeSpaceBytes_FolderExists_ReturnsFreeSpace()
    {
        // Act
        var result = fileSystemService.GetAvailableFreeSpaceBytes(rootPath);

        // Assert
        result.ShouldNotBeNull();
        result.Value.ShouldBeGreaterThan(0);
    }

    [Test]
    public void GetAvailableFreeSpaceBytes_FolderIsMissing_ReturnsNull()
    {
        // Act
        var result = fileSystemService.GetAvailableFreeSpaceBytes(
            Path.Combine(rootPath, "missing")
        );

        // Assert
        result.ShouldBeNull();
    }

    private sealed class RecordingTransferProgress : ITransferProgress
    {
        public long? TotalBytes { get; private set; }

        public long TransferredBytes { get; private set; }

        public void BeginFile(long? totalBytes)
        {
            TotalBytes = totalBytes;
        }

        public void ReportBytesTransferred(long bytes)
        {
            TransferredBytes += bytes;
        }
    }
}
