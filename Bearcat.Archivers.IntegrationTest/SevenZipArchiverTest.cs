using Bearcat.Archivers._7Zip;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Bearcat.Archivers.IntegrationTest;

public class SevenZipArchiverTest
{
    private SevenZipArchiver service = null!;
    private string destinationPath = null!;
    private string sourceFolderPath = null!;
    private string tempRootPath = null!;

    [SetUp]
    public void Setup()
    {
        service = new SevenZipArchiver(NullLogger<SevenZipArchiver>.Instance);
        tempRootPath = Path.Combine(Path.GetTempPath(), $"bearcat-7zip-tests-{Guid.NewGuid():N}");
        sourceFolderPath = Directory.CreateDirectory(Path.Combine(tempRootPath, "source")).FullName;
        destinationPath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, "destination"))
            .FullName;
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(tempRootPath))
        {
            Directory.Delete(tempRootPath, recursive: true);
        }
    }

    [Test]
    public async Task ArchiveAsync_SourceFolderContainsFiles_CreatesSevenZipArchive()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(sourceFolderPath, "first.txt"), "first");
        await File.WriteAllTextAsync(Path.Combine(sourceFolderPath, "second.txt"), "second");

        // Act
        var result = await service.ArchiveAsync(
            sourceFolderPath,
            destinationPath,
            "archive",
            1,
            null,
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessages.ShouldBeNull();
        result.CreatedFileNames.ShouldNotBeEmpty();
        result.CreatedFileNames.ShouldAllBe(f => File.Exists(f));
        result.CreatedFileNames.ShouldAllBe(f => f.StartsWith(destinationPath));
        result.CreatedFileNames.ShouldAllBe(f => Path.GetFileName(f).StartsWith("archive.7z"));
    }

    [Test]
    public async Task ArchiveAsync_SourceFolderDoesNotExist_ReturnsFailedResult()
    {
        // Arrange
        var missingSourceFolderPath = Path.Combine(tempRootPath, "missing-source");

        // Act
        var result = await service.ArchiveAsync(
            missingSourceFolderPath,
            destinationPath,
            "archive",
            1,
            null,
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.CreatedFileNames.ShouldBeEmpty();
        result.ErrorMessages.ShouldNotBeNull();
    }
}
