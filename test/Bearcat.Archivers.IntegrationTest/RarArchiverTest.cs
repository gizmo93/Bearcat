using Bearcat.Abstractions.Archiver;
using Bearcat.Archivers.Rar;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Bearcat.Archivers.IntegrationTest;

public class RarArchiverTest
{
    private RarArchiver service = null!;
    private string destinationPath = null!;
    private string sourceFolderPath = null!;
    private string tempRootPath = null!;

    [SetUp]
    public void Setup()
    {
        service = new RarArchiver(
            NullLogger<RarArchiver>.Instance,
            ArchiverTestConfiguration.Create()
        );
        tempRootPath = Path.Combine(Path.GetTempPath(), $"bearcat-rar-tests-{Guid.NewGuid():N}");
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
    public async Task ArchiveAsync_SourceFolderContainsFiles_CreatesRarArchive()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(sourceFolderPath, "first.txt"), "first");
        await File.WriteAllTextAsync(Path.Combine(sourceFolderPath, "second.txt"), "second");

        // Act
        var result = await service.ArchiveAsync(
            sourceFolderPath + Path.DirectorySeparatorChar,
            destinationPath,
            "archive",
            1,
            null,
            new ArchiveOptions(UseCompression: false, UseSolidArchive: false),
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        service.CanChangeHashInPlace.ShouldBeTrue();
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessages.ShouldBeNull();
        result.CreatedFileNames.ShouldNotBeEmpty();
        result.CreatedFileNames.ShouldAllBe(f => File.Exists(f));
        result.CreatedFileNames.ShouldAllBe(f => f.StartsWith(destinationPath));
        result.CreatedFileNames.ShouldAllBe(f => Path.GetFileName(f).StartsWith("archive"));
        result.CreatedFileNames.ShouldAllBe(f => f.EndsWith(".rar"));
    }

    [Test]
    public async Task ArchiveAsync_SourceFolderContainsFiles_ExtractsIntoSourceFolderName()
    {
        // Arrange
        const string fileName = "release.nfo";
        await File.WriteAllTextAsync(Path.Combine(sourceFolderPath, fileName), "release");
        var extractPath = Directory.CreateDirectory(Path.Combine(tempRootPath, "extract")).FullName;

        var result = await service.ArchiveAsync(
            sourceFolderPath + Path.DirectorySeparatorChar,
            destinationPath,
            "archive",
            1,
            null,
            new ArchiveOptions(UseCompression: false, UseSolidArchive: false),
            CancellationToken.None
        );

        result.IsSuccess.ShouldBeTrue();

        // Act
        await ArchiveExtractionTestHelper.ExtractWithRarAsync(
            result.CreatedFileNames.Order(StringComparer.Ordinal).First(),
            extractPath
        );

        // Assert
        ArchiveExtractionTestHelper.ExtractedFolderShouldMatchSourceFolder(
            sourceFolderPath,
            extractPath,
            fileName
        );
    }

    [Test]
    public async Task ArchiveAsync_CreatedFilesHaveTrailingNullByteAdded_CanStillBeExtracted()
    {
        // Arrange
        var sourceFilePath = await ArchiveExtractionTestHelper.WriteRandomSourceFileAsync(
            sourceFolderPath
        );
        var extractPath = Directory.CreateDirectory(Path.Combine(tempRootPath, "extract")).FullName;

        var result = await service.ArchiveAsync(
            sourceFolderPath,
            destinationPath,
            "archive",
            1,
            null,
            new ArchiveOptions(UseCompression: false, UseSolidArchive: false),
            CancellationToken.None
        );

        result.IsSuccess.ShouldBeTrue();
        result.CreatedFileNames.Count.ShouldBeGreaterThan(1);

        // Act
        await ArchiveExtractionTestHelper.AppendNullByteToEachArchiveFileAsync(result);
        await ArchiveExtractionTestHelper.ExtractWithRarAsync(
            result.CreatedFileNames.Order(StringComparer.Ordinal).First(),
            extractPath
        );

        // Assert
        ArchiveExtractionTestHelper.ExtractedPayloadShouldMatchSource(sourceFilePath, extractPath);
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
            new ArchiveOptions(UseCompression: false, UseSolidArchive: false),
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.CreatedFileNames.ShouldBeEmpty();
        result.ErrorMessages.ShouldNotBeNull();
    }
}
