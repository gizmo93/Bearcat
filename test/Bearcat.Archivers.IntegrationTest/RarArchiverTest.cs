using Bearcat.Abstractions.Archiver;
using Bearcat.Archivers.Rar;
using Bearcat.Archivers.Shared;
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
            ArchiverTestConfiguration.Create(),
            new ArchiverProcessRunner(NullLogger<ArchiverProcessRunner>.Instance)
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
    public async Task ArchiveAsync_SingleSplitArchiveFileHasTrailingNullByteAdded_CanStillBeExtracted()
    {
        // Arrange
        var sourceFilePath = await ArchiveExtractionTestHelper.WriteRandomSourceFileAsync(
            sourceFolderPath
        );
        var extractPath = Directory.CreateDirectory(Path.Combine(tempRootPath, "extract")).FullName;

        var result = await service.ArchiveAsync(
            sourceFolderPath: sourceFolderPath,
            destinationPath: destinationPath,
            archiveNamePrefix: "archive",
            targetFileSizeMb: 1,
            password: null,
            options: new ArchiveOptions(UseCompression: false, UseSolidArchive: false),
            cancellationToken: CancellationToken.None
        );

        result.IsSuccess.ShouldBeTrue();
        result.CreatedFileNames.Count.ShouldBeGreaterThan(1);

        var orderedArchiveFiles = result.CreatedFileNames.Order(StringComparer.Ordinal).ToList();

        // Act
        await ArchiveExtractionTestHelper.AppendNullByteToArchiveFileAsync(
            orderedArchiveFiles.Last()
        );
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
            new ArchiveOptions(UseCompression: false, UseSolidArchive: false),
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.CreatedFileNames.ShouldBeEmpty();
        result.ErrorMessages.ShouldNotBeNull();
    }

    [Test]
    public void FindArchivesToExtract_NewVolumeNamingWithUnrelatedFiles_ReturnsAllVolumesInOrder()
    {
        // Arrange
        ArchiveExtractionTestHelper.CreateEmptyFiles(
            sourceFolderPath,
            [
                "release.part2.rar",
                "release.part10.rar",
                "release.part1.rar",
                "release.part3.rar",
                "release.part4.rar",
                "release.part5.rar",
                "release.part6.rar",
                "release.part7.rar",
                "release.part8.rar",
                "release.part9.rar",
                "release.nfo",
                "release.sfv",
                "sample.mkv",
            ]
        );

        // Act
        var archives = service.FindArchivesToExtract(sourceFolderPath);

        // Assert
        var archive = archives.ShouldHaveSingleItem();
        archive.FirstVolumeFilePath.ShouldBe(Path.Combine(sourceFolderPath, "release.part1.rar"));
        archive.VolumeFilePaths.ShouldBe(
            Enumerable
                .Range(1, 10)
                .Select(partNumber =>
                    Path.Combine(sourceFolderPath, $"release.part{partNumber}.rar")
                )
                .ToList()
        );
    }

    [Test]
    public void FindArchivesToExtract_NewVolumeNamingWithZeroPaddedPartNumbers_UsesPartOneAsFirstVolume()
    {
        // Arrange
        ArchiveExtractionTestHelper.CreateEmptyFiles(
            sourceFolderPath,
            ["release.part002.rar", "release.part001.rar", "release.part003.rar"]
        );

        // Act
        var archives = service.FindArchivesToExtract(sourceFolderPath);

        // Assert
        var archive = archives.ShouldHaveSingleItem();
        archive.FirstVolumeFilePath.ShouldBe(Path.Combine(sourceFolderPath, "release.part001.rar"));
        archive.VolumeFilePaths.ShouldBe([
            Path.Combine(sourceFolderPath, "release.part001.rar"),
            Path.Combine(sourceFolderPath, "release.part002.rar"),
            Path.Combine(sourceFolderPath, "release.part003.rar"),
        ]);
    }

    [Test]
    public void FindArchivesToExtract_OldVolumeNamingWithUnrelatedFiles_ReturnsRarFileFirstAndContinuationVolumesInOrder()
    {
        // Arrange
        ArchiveExtractionTestHelper.CreateEmptyFiles(
            sourceFolderPath,
            [
                "release.s00",
                "release.r01",
                "release.rar",
                "release.r99",
                "release.r00",
                "release.nfo",
                "release.sfv",
                "sample.mkv",
            ]
        );

        // Act
        var archives = service.FindArchivesToExtract(sourceFolderPath);

        // Assert
        var archive = archives.ShouldHaveSingleItem();
        archive.FirstVolumeFilePath.ShouldBe(Path.Combine(sourceFolderPath, "release.rar"));
        archive.VolumeFilePaths.ShouldBe([
            Path.Combine(sourceFolderPath, "release.rar"),
            Path.Combine(sourceFolderPath, "release.r00"),
            Path.Combine(sourceFolderPath, "release.r01"),
            Path.Combine(sourceFolderPath, "release.r99"),
            Path.Combine(sourceFolderPath, "release.s00"),
        ]);
    }

    [Test]
    public void FindArchivesToExtract_SingleRarFile_ReturnsArchiveWithOneVolume()
    {
        // Arrange
        ArchiveExtractionTestHelper.CreateEmptyFiles(sourceFolderPath, ["release.rar"]);

        // Act
        var archives = service.FindArchivesToExtract(sourceFolderPath);

        // Assert
        var archive = archives.ShouldHaveSingleItem();
        archive.FirstVolumeFilePath.ShouldBe(Path.Combine(sourceFolderPath, "release.rar"));
        archive.VolumeFilePaths.ShouldBe([Path.Combine(sourceFolderPath, "release.rar")]);
    }

    [Test]
    public void FindArchivesToExtract_TwoIndependentArchives_ReturnsBothArchivesWithTheirOwnVolumes()
    {
        // Arrange
        ArchiveExtractionTestHelper.CreateEmptyFiles(
            sourceFolderPath,
            [
                "first.part1.rar",
                "first.part2.rar",
                "second.rar",
                "second.r00",
                "third.part1.rar",
                "third.part2.rar",
            ]
        );

        // Act
        var archives = service.FindArchivesToExtract(sourceFolderPath);

        // Assert
        archives.Count.ShouldBe(3);
        archives[0]
            .VolumeFilePaths.ShouldBe([
                Path.Combine(sourceFolderPath, "first.part1.rar"),
                Path.Combine(sourceFolderPath, "first.part2.rar"),
            ]);
        archives[1]
            .VolumeFilePaths.ShouldBe([
                Path.Combine(sourceFolderPath, "second.rar"),
                Path.Combine(sourceFolderPath, "second.r00"),
            ]);
        archives[2]
            .VolumeFilePaths.ShouldBe([
                Path.Combine(sourceFolderPath, "third.part1.rar"),
                Path.Combine(sourceFolderPath, "third.part2.rar"),
            ]);
    }

    [Test]
    public void FindArchivesToExtract_FileNamesDifferInCase_GroupsVolumesCaseInsensitively()
    {
        // Arrange
        ArchiveExtractionTestHelper.CreateEmptyFiles(
            sourceFolderPath,
            ["Release.PART1.RAR", "release.Part2.Rar", "Old.RAR", "old.R00", "OLD.s00"]
        );

        // Act
        var archives = service.FindArchivesToExtract(sourceFolderPath);

        // Assert
        archives.Count.ShouldBe(2);
        archives[0]
            .VolumeFilePaths.ShouldBe([
                Path.Combine(sourceFolderPath, "Old.RAR"),
                Path.Combine(sourceFolderPath, "old.R00"),
                Path.Combine(sourceFolderPath, "OLD.s00"),
            ]);
        archives[1]
            .VolumeFilePaths.ShouldBe([
                Path.Combine(sourceFolderPath, "Release.PART1.RAR"),
                Path.Combine(sourceFolderPath, "release.Part2.Rar"),
            ]);
    }

    [Test]
    public void FindArchivesToExtract_SplitZipVolumes_ReturnsNoArchivesAndDoesNotThrow()
    {
        // Arrange
        ArchiveExtractionTestHelper.CreateEmptyFiles(sourceFolderPath, ["x.zip", "x.z01"]);

        // Act
        var archives = service.FindArchivesToExtract(sourceFolderPath);

        // Assert
        archives.ShouldBeEmpty();
    }

    [Test]
    public void FindArchivesToExtract_FolderContainsNoArchives_ReturnsEmptyList()
    {
        // Arrange
        ArchiveExtractionTestHelper.CreateEmptyFiles(
            sourceFolderPath,
            ["release.nfo", "release.sfv", "sample.mkv"]
        );
        var subfolderPath = Directory
            .CreateDirectory(Path.Combine(sourceFolderPath, "Subs"))
            .FullName;
        ArchiveExtractionTestHelper.CreateEmptyFiles(subfolderPath, ["subs.rar"]);

        // Act
        var archives = service.FindArchivesToExtract(sourceFolderPath);

        // Assert
        archives.ShouldBeEmpty();
    }

    [Test]
    public void FindArchivesToExtract_NewVolumeNamingFirstVolumeMissing_Throws()
    {
        // Arrange
        ArchiveExtractionTestHelper.CreateEmptyFiles(
            sourceFolderPath,
            ["release.part2.rar", "release.part3.rar"]
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
    public void FindArchivesToExtract_OldVolumeNamingFirstVolumeMissing_Throws()
    {
        // Arrange
        ArchiveExtractionTestHelper.CreateEmptyFiles(
            sourceFolderPath,
            ["release.r00", "release.r01", "release.nfo"]
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
    public async Task ExtractAsync_NewVolumeNamingMultiVolumeArchive_ExtractsContentEqualToSource()
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
            options: new ArchiveOptions(UseCompression: false, UseSolidArchive: false),
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
        archiveToExtract.VolumeFilePaths.ShouldBe(
            archiveResult.CreatedFileNames.Order(StringComparer.Ordinal).ToList()
        );
        ArchiveExtractionTestHelper.ExtractedPayloadShouldMatchSource(sourceFilePath, extractPath);
    }

    [Test]
    public async Task ExtractAsync_OldVolumeNamingMultiVolumeArchive_ExtractsContentEqualToSource()
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
            options: new ArchiveOptions(UseCompression: false, UseSolidArchive: false),
            cancellationToken: CancellationToken.None
        );
        archiveResult.IsSuccess.ShouldBeTrue();
        var oldNamingVolumeFilePaths = RenameVolumesToOldVolumeNaming(
            archiveResult.CreatedFileNames
        );

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
        archiveToExtract.FirstVolumeFilePath.ShouldBe(Path.Combine(destinationPath, "archive.rar"));
        archiveToExtract.VolumeFilePaths.ShouldBe(oldNamingVolumeFilePaths);
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
            options: new ArchiveOptions(UseCompression: false, UseSolidArchive: false),
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
            options: new ArchiveOptions(UseCompression: false, UseSolidArchive: false),
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

    private static List<string> RenameVolumesToOldVolumeNaming(
        IReadOnlyList<string> newNamingVolumeFilePaths
    )
    {
        var orderedNewNamingVolumeFilePaths = newNamingVolumeFilePaths
            .Order(StringComparer.Ordinal)
            .ToList();
        var oldNamingVolumeFilePaths = new List<string>();

        for (
            var volumeIndex = 0;
            volumeIndex < orderedNewNamingVolumeFilePaths.Count;
            volumeIndex++
        )
        {
            var folderPath = Path.GetDirectoryName(orderedNewNamingVolumeFilePaths[volumeIndex])!;
            var oldNamingFileName =
                volumeIndex == 0 ? "archive.rar" : $"archive.r{volumeIndex - 1:00}";
            var oldNamingVolumeFilePath = Path.Combine(folderPath, oldNamingFileName);

            File.Move(orderedNewNamingVolumeFilePaths[volumeIndex], oldNamingVolumeFilePath);
            oldNamingVolumeFilePaths.Add(oldNamingVolumeFilePath);
        }

        return oldNamingVolumeFilePaths;
    }
}
