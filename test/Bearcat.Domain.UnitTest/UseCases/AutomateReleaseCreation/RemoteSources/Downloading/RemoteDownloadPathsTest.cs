using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Downloading;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.AutomateReleaseCreation.RemoteSources.Downloading;

public class RemoteDownloadPathsTest
{
    private static readonly string TargetPath = Path.Combine(
        Path.GetTempPath(),
        "bearcat-downloads"
    );

    private static readonly string LocalFolderPath = Path.Combine(TargetPath, "Release-GRP");

    [TestCase("release.rar", new[] { "release.rar" })]
    [TestCase("Subs/english.srt", new[] { "Subs", "english.srt" })]
    [TestCase("Sample/nested/sample.mkv", new[] { "Sample", "nested", "sample.mkv" })]
    [TestCase("..release.nfo", new[] { "..release.nfo" })]
    public void ResolveLocalFilePath_SafeRelativePath_MapsSegmentsBelowLocalFolder(
        string relativePath,
        string[] expectedSegments
    )
    {
        // Act
        var localFilePath = RemoteDownloadPaths.ResolveLocalFilePath(LocalFolderPath, relativePath);

        // Assert
        localFilePath.ShouldBe(Path.Combine([LocalFolderPath, .. expectedSegments]));
    }

    [TestCase("")]
    [TestCase("/etc/passwd")]
    [TestCase("../escape.rar")]
    [TestCase("Subs/../../escape.rar")]
    [TestCase("./release.rar")]
    [TestCase("Subs//english.srt")]
    [TestCase("Subs/")]
    [TestCase("..\\escape.rar")]
    [TestCase("Subs\\english.srt")]
    [TestCase("null\0byte.rar")]
    [TestCase("...")]
    public void ResolveLocalFilePath_UnsafeRelativePath_ReturnsNull(string relativePath)
    {
        // Act
        var localFilePath = RemoteDownloadPaths.ResolveLocalFilePath(LocalFolderPath, relativePath);

        // Assert
        localFilePath.ShouldBeNull();
    }

    [Test]
    public void CanDeleteDownloadFolder_FolderInsideTargetPath_ReturnsTrue()
    {
        // Arrange
        var download = CreateDownload("Release-GRP", LocalFolderPath, TargetPath);

        // Act
        var canDelete = RemoteDownloadPaths.CanDeleteDownloadFolder(download);

        // Assert
        canDelete.ShouldBeTrue();
    }

    [Test]
    public void CanDeleteDownloadFolder_AutomationWasDeleted_ReturnsTrueForRecordedDownloadFolder()
    {
        // Arrange
        var download = CreateDownload("Release-GRP", LocalFolderPath, targetPath: null);

        // Act
        var canDelete = RemoteDownloadPaths.CanDeleteDownloadFolder(download);

        // Assert
        canDelete.ShouldBeTrue();
    }

    [Test]
    public void CanDeleteDownloadFolder_FolderOutsideTargetPath_ReturnsFalse()
    {
        // Arrange
        var download = CreateDownload(
            "Release-GRP",
            Path.Combine(Path.GetTempPath(), "elsewhere", "Release-GRP"),
            TargetPath
        );

        // Act
        var canDelete = RemoteDownloadPaths.CanDeleteDownloadFolder(download);

        // Assert
        canDelete.ShouldBeFalse();
    }

    [Test]
    public void CanDeleteDownloadFolder_FolderIsTargetPathItself_ReturnsFalse()
    {
        // Arrange
        var download = CreateDownload("bearcat-downloads", TargetPath, TargetPath);

        // Act
        var canDelete = RemoteDownloadPaths.CanDeleteDownloadFolder(download);

        // Assert
        canDelete.ShouldBeFalse();
    }

    [TestCase("..")]
    [TestCase("Other-GRP")]
    public void CanDeleteDownloadFolder_FolderNameDoesNotMatchPath_ReturnsFalse(string folderName)
    {
        // Arrange
        var download = CreateDownload(folderName, LocalFolderPath, targetPath: null);

        // Act
        var canDelete = RemoteDownloadPaths.CanDeleteDownloadFolder(download);

        // Assert
        canDelete.ShouldBeFalse();
    }

    private static RemoteSourceDownload CreateDownload(
        string folderName,
        string localFolderPath,
        string? targetPath
    )
    {
        return new RemoteSourceDownload
        {
            SourceName = "Main FTP",
            RemoteFolderPath = $"/incoming/{folderName}",
            FolderName = folderName,
            LocalFolderPath = localFolderPath,
            RemoteSourceAutomation = targetPath is null
                ? null
                : new RemoteSourceAutomation
                {
                    Name = "Automation",
                    RemotePath = "/incoming",
                    TargetPath = targetPath,
                },
        };
    }
}
