using Bearcat.Abstractions.Archiver;
using Bearcat.Archivers._7Zip;
using Bearcat.Archivers.Shared;
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
        service = new SevenZipArchiver(
            ArchiverTestConfiguration.Create(),
            new ArchiverProcessRunner(NullLogger<ArchiverProcessRunner>.Instance)
        );
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
            sourceFolderPath + Path.DirectorySeparatorChar,
            destinationPath,
            "archive",
            1,
            null,
            new ArchiveOptions(
                UseCompression: false,
                UseSolidArchive: false,
                PackSourceFolderAsRootFolder: true
            ),
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        service.CanChangeHashInPlace.ShouldBeFalse();
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessages.ShouldBeNull();
        result.CreatedFileNames.ShouldNotBeEmpty();
        result.CreatedFileNames.ShouldAllBe(f => File.Exists(f));
        result.CreatedFileNames.ShouldAllBe(f => f.StartsWith(destinationPath));
        result.CreatedFileNames.ShouldAllBe(f => Path.GetFileName(f).StartsWith("archive.7z"));
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
            new ArchiveOptions(
                UseCompression: false,
                UseSolidArchive: false,
                PackSourceFolderAsRootFolder: true
            ),
            CancellationToken.None
        );

        result.IsSuccess.ShouldBeTrue();

        // Act
        var extractionResult = await service.ExtractAsync(
            service.FindArchivesToExtract(destinationPath).Single(),
            extractPath,
            CancellationToken.None
        );

        // Assert
        extractionResult.IsSuccess.ShouldBeTrue();
        ArchiveExtractionTestHelper.ExtractedFolderShouldMatchSourceFolder(
            sourceFolderPath,
            extractPath,
            fileName
        );
    }

    [Test]
    public async Task ArchiveAsync_PackSourceFolderAsRootFolderEnabled_ExtractsFolderStructureIntoSourceFolderName()
    {
        // Arrange
        await ArchiveExtractionTestHelper.CreateNestedFoldersWithHiddenFilesAndEmptyFoldersAsync(
            sourceFolderPath
        );
        var extractPath = Directory.CreateDirectory(Path.Combine(tempRootPath, "extract")).FullName;

        var result = await service.ArchiveAsync(
            sourceFolderPath + Path.DirectorySeparatorChar,
            destinationPath,
            "archive",
            1,
            null,
            new ArchiveOptions(
                UseCompression: false,
                UseSolidArchive: false,
                PackSourceFolderAsRootFolder: true
            ),
            CancellationToken.None
        );

        result.IsSuccess.ShouldBeTrue();

        // Act
        var extractionResult = await service.ExtractAsync(
            service.FindArchivesToExtract(destinationPath).Single(),
            extractPath,
            CancellationToken.None
        );

        // Assert
        extractionResult.IsSuccess.ShouldBeTrue();
        Directory.GetFileSystemEntries(extractPath).Select(Path.GetFileName).ShouldBe(["source"]);
        ArchiveExtractionTestHelper.ExtractedFolderShouldMatchSourceFolderStructure(
            sourceFolderPath,
            Path.Combine(extractPath, "source")
        );
    }

    [Test]
    public async Task ArchiveAsync_PackSourceFolderAsRootFolderDisabled_ExtractsFolderStructureWithoutSourceFolderName()
    {
        // Arrange
        await ArchiveExtractionTestHelper.CreateNestedFoldersWithHiddenFilesAndEmptyFoldersAsync(
            sourceFolderPath
        );
        var extractPath = Directory.CreateDirectory(Path.Combine(tempRootPath, "extract")).FullName;

        var result = await service.ArchiveAsync(
            sourceFolderPath + Path.DirectorySeparatorChar,
            destinationPath,
            "archive",
            1,
            null,
            new ArchiveOptions(
                UseCompression: false,
                UseSolidArchive: false,
                PackSourceFolderAsRootFolder: false
            ),
            CancellationToken.None
        );

        result.IsSuccess.ShouldBeTrue();

        // Act
        var extractionResult = await service.ExtractAsync(
            service.FindArchivesToExtract(destinationPath).Single(),
            extractPath,
            CancellationToken.None
        );

        // Assert
        extractionResult.IsSuccess.ShouldBeTrue();
        ArchiveExtractionTestHelper.ExtractedFolderShouldMatchSourceFolderStructure(
            sourceFolderPath,
            extractPath
        );
    }

    [Test]
    public async Task ArchiveAsync_SingleVolumeFileHasTrailingNullByteAdded_CanStillBeExtracted()
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
            10,
            null,
            new ArchiveOptions(
                UseCompression: false,
                UseSolidArchive: false,
                PackSourceFolderAsRootFolder: true
            ),
            CancellationToken.None
        );

        result.IsSuccess.ShouldBeTrue();
        result.CreatedFileNames.Count.ShouldBe(1);

        // Act
        await ArchiveExtractionTestHelper.AppendNullByteToEachArchiveFileAsync(result);
        var extractionResult = await service.ExtractAsync(
            service.FindArchivesToExtract(destinationPath).Single(),
            extractPath,
            CancellationToken.None
        );

        // Assert
        extractionResult.IsSuccess.ShouldBeTrue();
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
            new ArchiveOptions(
                UseCompression: false,
                UseSolidArchive: false,
                PackSourceFolderAsRootFolder: true
            ),
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.CreatedFileNames.ShouldBeEmpty();
        result.ErrorMessages.ShouldNotBeNull();
    }

    [Test]
    public void FindArchivesToExtract_SingleVolumeArchiveWithUnrelatedFiles_ReturnsArchiveWithOneVolume()
    {
        // Arrange
        ArchiveExtractionTestHelper.CreateEmptyFiles(
            sourceFolderPath,
            ["release.7z", "release.nfo", "release.sfv", "sample.mkv"]
        );

        // Act
        var archives = service.FindArchivesToExtract(sourceFolderPath);

        // Assert
        var archive = archives.ShouldHaveSingleItem();
        archive.FirstVolumeFilePath.ShouldBe(Path.Combine(sourceFolderPath, "release.7z"));
        archive.VolumeFilePaths.ShouldBe([Path.Combine(sourceFolderPath, "release.7z")]);
    }

    [Test]
    public void FindArchivesToExtract_MultiVolumeArchiveWithUnrelatedFiles_ReturnsAllVolumesInOrder()
    {
        // Arrange
        ArchiveExtractionTestHelper.CreateEmptyFiles(
            sourceFolderPath,
            ["release.7z.003", "release.7z.001", "release.7z.002", "release.nfo", "sample.mkv"]
        );

        // Act
        var archives = service.FindArchivesToExtract(sourceFolderPath);

        // Assert
        var archive = archives.ShouldHaveSingleItem();
        archive.FirstVolumeFilePath.ShouldBe(Path.Combine(sourceFolderPath, "release.7z.001"));
        archive.VolumeFilePaths.ShouldBe([
            Path.Combine(sourceFolderPath, "release.7z.001"),
            Path.Combine(sourceFolderPath, "release.7z.002"),
            Path.Combine(sourceFolderPath, "release.7z.003"),
        ]);
    }

    [Test]
    public void FindArchivesToExtract_TwoIndependentArchives_ReturnsBothArchivesWithTheirOwnVolumes()
    {
        // Arrange
        ArchiveExtractionTestHelper.CreateEmptyFiles(
            sourceFolderPath,
            ["first.7z.001", "first.7z.002", "second.7z"]
        );

        // Act
        var archives = service.FindArchivesToExtract(sourceFolderPath);

        // Assert
        archives.Count.ShouldBe(2);
        archives[0]
            .VolumeFilePaths.ShouldBe([
                Path.Combine(sourceFolderPath, "first.7z.001"),
                Path.Combine(sourceFolderPath, "first.7z.002"),
            ]);
        archives[1].VolumeFilePaths.ShouldBe([Path.Combine(sourceFolderPath, "second.7z")]);
    }

    [Test]
    public void FindArchivesToExtract_FileNamesDifferInCase_GroupsVolumesCaseInsensitively()
    {
        // Arrange
        ArchiveExtractionTestHelper.CreateEmptyFiles(
            sourceFolderPath,
            ["Release.7Z.001", "release.7z.002", "Single.7Z"]
        );

        // Act
        var archives = service.FindArchivesToExtract(sourceFolderPath);

        // Assert
        archives.Count.ShouldBe(2);
        archives[0]
            .VolumeFilePaths.ShouldBe([
                Path.Combine(sourceFolderPath, "Release.7Z.001"),
                Path.Combine(sourceFolderPath, "release.7z.002"),
            ]);
        archives[1].VolumeFilePaths.ShouldBe([Path.Combine(sourceFolderPath, "Single.7Z")]);
    }

    [Test]
    public void FindArchivesToExtract_FolderContainsNoArchives_ReturnsEmptyList()
    {
        // Arrange
        ArchiveExtractionTestHelper.CreateEmptyFiles(
            sourceFolderPath,
            ["release.nfo", "release.part1.rar", "sample.mkv"]
        );

        // Act
        var archives = service.FindArchivesToExtract(sourceFolderPath);

        // Assert
        archives.ShouldBeEmpty();
    }

    [Test]
    public void FindArchivesToExtract_FirstVolumeMissing_Throws()
    {
        // Arrange
        ArchiveExtractionTestHelper.CreateEmptyFiles(
            sourceFolderPath,
            ["release.7z.002", "release.7z.003"]
        );

        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            service.FindArchivesToExtract(sourceFolderPath)
        );

        // Assert
        exception.Message.ShouldContain(sourceFolderPath);
        exception.Message.ShouldContain("'release'");
    }

    [Test]
    public async Task ExtractAsync_MultiVolumeArchive_ExtractsContentEqualToSource()
    {
        // Arrange
        var sourceFilePath = await ArchiveExtractionTestHelper.WriteRandomSourceFileAsync(
            sourceFolderPath
        );
        var extractPath = Path.Combine(tempRootPath, "extract");

        var archiveResult = await service.ArchiveAsync(
            sourceFolderPath: sourceFolderPath,
            destinationPath: destinationPath,
            archiveNamePrefix: "archive",
            targetFileSizeMb: 1,
            password: null,
            options: new ArchiveOptions(
                UseCompression: false,
                UseSolidArchive: false,
                PackSourceFolderAsRootFolder: true
            ),
            cancellationToken: CancellationToken.None
        );
        archiveResult.IsSuccess.ShouldBeTrue();
        archiveResult.CreatedFileNames.Count.ShouldBeGreaterThan(1);

        var archiveToExtract = service
            .FindArchivesToExtract(destinationPath)
            .ShouldHaveSingleItem();

        // Act
        var extractionResult = await service.ExtractAsync(
            archiveToExtract,
            extractPath,
            CancellationToken.None
        );

        // Assert
        extractionResult.IsSuccess.ShouldBeTrue();
        extractionResult.ErrorMessages.ShouldBeEmpty();
        archiveToExtract.FirstVolumeFilePath.ShouldBe(
            Path.Combine(destinationPath, "archive.7z.001")
        );
        archiveToExtract.VolumeFilePaths.ShouldBe(
            archiveResult.CreatedFileNames.Order(StringComparer.Ordinal).ToList()
        );
        ArchiveExtractionTestHelper.ExtractedPayloadShouldMatchSource(sourceFilePath, extractPath);
    }

    [Test]
    public async Task ExtractAsync_DestinationFileAlreadyExists_DoesNotOverwriteExistingFile()
    {
        // Arrange
        const string fileName = "release.nfo";
        await File.WriteAllTextAsync(Path.Combine(sourceFolderPath, fileName), "release");
        var extractPath = Path.Combine(tempRootPath, "extract");
        var existingFolderPath = Directory
            .CreateDirectory(Path.Combine(extractPath, "source"))
            .FullName;
        await File.WriteAllTextAsync(Path.Combine(existingFolderPath, fileName), "existing");

        var archiveResult = await service.ArchiveAsync(
            sourceFolderPath: sourceFolderPath,
            destinationPath: destinationPath,
            archiveNamePrefix: "archive",
            targetFileSizeMb: 1,
            password: null,
            options: new ArchiveOptions(
                UseCompression: false,
                UseSolidArchive: false,
                PackSourceFolderAsRootFolder: true
            ),
            cancellationToken: CancellationToken.None
        );
        archiveResult.IsSuccess.ShouldBeTrue();

        // Act
        var extractionResult = await service.ExtractAsync(
            service.FindArchivesToExtract(destinationPath).Single(),
            extractPath,
            CancellationToken.None
        );

        // Assert
        extractionResult.IsSuccess.ShouldBeTrue();
        (await File.ReadAllTextAsync(Path.Combine(existingFolderPath, fileName))).ShouldBe(
            "existing"
        );
    }

    [Test]
    public async Task ExtractAsync_PasswordProtectedArchive_ReturnsFailedResultWithoutWaitingForPassword()
    {
        // Arrange
        await ArchiveExtractionTestHelper.WriteRandomSourceFileAsync(sourceFolderPath);
        var extractPath = Path.Combine(tempRootPath, "extract");

        var archiveResult = await service.ArchiveAsync(
            sourceFolderPath: sourceFolderPath,
            destinationPath: destinationPath,
            archiveNamePrefix: "archive",
            targetFileSizeMb: 1,
            password: "secret",
            options: new ArchiveOptions(
                UseCompression: false,
                UseSolidArchive: false,
                PackSourceFolderAsRootFolder: true
            ),
            cancellationToken: CancellationToken.None
        );
        archiveResult.IsSuccess.ShouldBeTrue();
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act
        var extractionResult = await service.ExtractAsync(
            service.FindArchivesToExtract(destinationPath).Single(),
            extractPath,
            cancellationTokenSource.Token
        );

        // Assert
        extractionResult.IsSuccess.ShouldBeFalse();
        extractionResult.ErrorMessages.ShouldNotBeEmpty();
    }
}
