using System.IO.Hashing;
using System.Text;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.VerificationAndExtraction.SfvVerification;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.AutomateReleaseCreation.RemoteSources.VerificationAndExtraction;

public class SfvChecksumVerifierTest
{
    private string localFolderPath = null!;
    private RemoteSourceDownload download = null!;
    private RecordingTransferProgressTracker progressTracker = null!;
    private SfvChecksumVerifier verifier = null!;

    [SetUp]
    public void SetUp()
    {
        localFolderPath = Directory
            .CreateDirectory(Path.Combine(Path.GetTempPath(), $"bearcat-sfv-{Guid.NewGuid():N}"))
            .FullName;
        download = new RemoteSourceDownload
        {
            Id = 7,
            SourceName = "Main FTP",
            RemoteFolderPath = "/incoming/release",
            FolderName = "release",
            LocalFolderPath = localFolderPath,
        };
        progressTracker = new RecordingTransferProgressTracker();
        verifier = new SfvChecksumVerifier(
            progressTracker,
            NullLogger<SfvChecksumVerifier>.Instance
        );
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(localFolderPath))
        {
            Directory.Delete(localFolderPath, recursive: true);
        }
    }

    [Test]
    public void FindSfvFiles_SfvFilesInSubfolders_FindsThemIgnoringCase()
    {
        // Arrange
        WriteFile("release.SFV", "");
        WriteFile(Path.Combine("Subs", "subs.sfv"), "");
        WriteFile("release.nfo", "");

        // Act
        var sfvFilePaths = SfvChecksumVerifier.FindSfvFiles(localFolderPath);

        // Assert
        sfvFilePaths.ShouldBe(
            [
                Path.Combine(localFolderPath, "Subs", "subs.sfv"),
                Path.Combine(localFolderPath, "release.SFV"),
            ],
            ignoreOrder: true
        );
    }

    [Test]
    public async Task VerifyAsync_AllChecksumsMatch_ReturnsParsedSfvFiles()
    {
        // Arrange
        WriteFile("release.rar", "first volume");
        WriteFile("release.r00", "second volume");
        var sfvFilePath = WriteFile(
            "release.sfv",
            CreateSfvLine("release.rar", "first volume"),
            CreateSfvLine("release.r00", "second volume")
        );

        // Act
        var sfvFiles = await verifier.VerifyAsync(download, [sfvFilePath], CancellationToken.None);

        // Assert
        var sfvFile = sfvFiles.ShouldHaveSingleItem();
        sfvFile.FilePath.ShouldBe(sfvFilePath);
        sfvFile
            .Entries.Select(entry => entry.RelativeFilePath)
            .ShouldBe(["release.rar", "release.r00"]);
    }

    [Test]
    public async Task VerifyAsync_AllChecksumsMatch_ReportsAllBytesOfAllEntriesAndStopsTracking()
    {
        // Arrange
        WriteFile("release.rar", "first volume");
        WriteFile(Path.Combine("Subs", "subs.rar"), "subtitles");
        var sfvFilePath = WriteFile(
            "release.sfv",
            CreateSfvLine("release.rar", "first volume"),
            CreateSfvLine("Subs/subs.rar", "subtitles")
        );
        var identifier = new TransferIdentifier(TransferType.RemoteDownloadVerification, 7);

        // Act
        await verifier.VerifyAsync(download, [sfvFilePath], CancellationToken.None);

        // Assert
        progressTracker
            .PlannedFilesPerIdentifier[identifier]
            .ShouldBe([
                new TransferFile(1, "release.rar", "Main FTP", 12, IsAlreadyTransferred: false),
                new TransferFile(2, "Subs/subs.rar", "Main FTP", 9, IsAlreadyTransferred: false),
            ]);
        var snapshot = progressTracker.LastSnapshotPerIdentifier[identifier];
        snapshot.TransferredBytes.ShouldBe(21);
        snapshot.TotalBytes.ShouldBe(21);
        snapshot
            .Files.Select(file => (file.FileName, file.TransferredBytes))
            .ShouldBe([("release.rar", 12L), ("Subs/subs.rar", 9L)], ignoreOrder: true);
        progressTracker.StoppedIdentifiers.ShouldBe([identifier]);
        progressTracker.Get(identifier).ShouldBeNull();
    }

    [Test]
    public async Task VerifyAsync_FileMissing_PlansMissingFileWithoutSizeAndStopsTracking()
    {
        // Arrange
        WriteFile("release.rar", "first volume");
        var sfvFilePath = WriteFile(
            "release.sfv",
            CreateSfvLine("release.rar", "first volume"),
            CreateSfvLine("release.r00", "second volume")
        );
        var identifier = new TransferIdentifier(TransferType.RemoteDownloadVerification, 7);

        // Act
        var verify = () => verifier.VerifyAsync(download, [sfvFilePath], CancellationToken.None);

        // Assert
        await Should.ThrowAsync<InvalidDataException>(verify);
        progressTracker
            .PlannedFilesPerIdentifier[identifier]
            .Select(file => (file.FileName, file.SizeBytes))
            .ShouldBe([("release.rar", (long?)12), ("release.r00", null)]);
        progressTracker.StoppedIdentifiers.ShouldBe([identifier]);
    }

    [Test]
    public async Task VerifyAsync_ChecksumMismatch_ThrowsWithRelativeFilePath()
    {
        // Arrange
        WriteFile("release.rar", "first volume");
        WriteFile(Path.Combine("Subs", "subs.rar"), "corrupted");
        var sfvFilePath = WriteFile(
            "release.sfv",
            CreateSfvLine("release.rar", "first volume"),
            CreateSfvLine("Subs/subs.rar", "subtitles")
        );

        // Act
        var verify = () => verifier.VerifyAsync(download, [sfvFilePath], CancellationToken.None);

        // Assert
        var exception = await Should.ThrowAsync<InvalidDataException>(verify);
        exception.Message.ShouldContain("wrong CRC32 checksum: Subs/subs.rar");
        exception.Message.ShouldNotContain("missing");
        exception.Message.ShouldNotContain("release.rar");
    }

    [Test]
    public async Task VerifyAsync_FileMissing_ThrowsWithRelativeFilePath()
    {
        // Arrange
        WriteFile("release.rar", "first volume");
        var sfvFilePath = WriteFile(
            "release.sfv",
            CreateSfvLine("release.rar", "first volume"),
            CreateSfvLine("release.r00", "second volume")
        );

        // Act
        var verify = () => verifier.VerifyAsync(download, [sfvFilePath], CancellationToken.None);

        // Assert
        var exception = await Should.ThrowAsync<InvalidDataException>(verify);
        exception.Message.ShouldContain("missing files: release.r00");
        exception.Message.ShouldNotContain("wrong CRC32 checksum");
    }

    [Test]
    public async Task VerifyAsync_ManyFilesMissing_ListsFirstTenAndCountsTheRest()
    {
        // Arrange
        var lines = Enumerable
            .Range(0, 12)
            .Select(index => CreateSfvLine($"release.r{index:00}", "volume"))
            .ToArray();
        var sfvFilePath = WriteFile("release.sfv", lines);

        // Act
        var verify = () => verifier.VerifyAsync(download, [sfvFilePath], CancellationToken.None);

        // Assert
        var exception = await Should.ThrowAsync<InvalidDataException>(verify);
        exception.Message.ShouldContain("release.r09 and 2 more");
        exception.Message.ShouldNotContain("release.r10");
    }

    [Test]
    public async Task VerifyAsync_FileNameWithSpaces_SplitsLineAtLastWhitespace()
    {
        // Arrange
        WriteFile("My Release Name.r00", "volume");
        var sfvFilePath = WriteFile("release.sfv", CreateSfvLine("My Release Name.r00", "volume"));

        // Act
        var sfvFiles = await verifier.VerifyAsync(download, [sfvFilePath], CancellationToken.None);

        // Assert
        var entry = sfvFiles.ShouldHaveSingleItem().Entries.ShouldHaveSingleItem();
        entry.RelativeFilePath.ShouldBe("My Release Name.r00");
        entry.FilePath.ShouldBe(Path.Combine(localFolderPath, "My Release Name.r00"));
        entry.ExpectedCrc32.ShouldBe(ComputeCrc32("volume"));
    }

    [Test]
    public async Task VerifyAsync_CommentsAndEmptyLines_AreIgnored()
    {
        // Arrange
        WriteFile("release.rar", "first volume");
        WriteFile("release.r00", "second volume");
        var sfvFilePath = WriteFile(
            "release.sfv",
            "; Generated by some tool",
            "",
            "   ",
            $"release.rar {ComputeCrc32("first volume"):x8}",
            "  ; indented comment",
            $"release.r00\t{ComputeCrc32("second volume"):X8}  "
        );

        // Act
        var sfvFiles = await verifier.VerifyAsync(download, [sfvFilePath], CancellationToken.None);

        // Assert
        sfvFiles
            .ShouldHaveSingleItem()
            .Entries.Select(entry => (entry.RelativeFilePath, entry.ExpectedCrc32))
            .ShouldBe([
                ("release.rar", ComputeCrc32("first volume")),
                ("release.r00", ComputeCrc32("second volume")),
            ]);
    }

    [Test]
    public async Task VerifyAsync_BackslashInFileName_IsTreatedAsFolderSeparator()
    {
        // Arrange
        WriteFile(Path.Combine("CD1", "release.rar"), "volume");
        var sfvFilePath = WriteFile("release.sfv", $@"CD1\release.rar {ComputeCrc32("volume"):X8}");

        // Act
        var sfvFiles = await verifier.VerifyAsync(download, [sfvFilePath], CancellationToken.None);

        // Assert
        var entry = sfvFiles.ShouldHaveSingleItem().Entries.ShouldHaveSingleItem();
        entry.RelativeFilePath.ShouldBe("CD1/release.rar");
        entry.FilePath.ShouldBe(Path.Combine(localFolderPath, "CD1", "release.rar"));
    }

    [Test]
    public async Task VerifyAsync_SfvFileInSubfolder_ResolvesEntriesRelativeToItsFolder()
    {
        // Arrange
        WriteFile(Path.Combine("Subs", "subs.rar"), "subtitles");
        var sfvFilePath = WriteFile(
            Path.Combine("Subs", "subs.sfv"),
            CreateSfvLine("subs.rar", "subtitles")
        );

        // Act
        var sfvFiles = await verifier.VerifyAsync(download, [sfvFilePath], CancellationToken.None);

        // Assert
        var entry = sfvFiles.ShouldHaveSingleItem().Entries.ShouldHaveSingleItem();
        entry.RelativeFilePath.ShouldBe("Subs/subs.rar");
        entry.FilePath.ShouldBe(Path.Combine(localFolderPath, "Subs", "subs.rar"));
    }

    [TestCase("release.rar")]
    [TestCase("1A2B3C4D")]
    [TestCase("release.rar 1A2B3C4")]
    [TestCase("release.rar 1A2B3C4D5")]
    [TestCase("release.rar XYZ12345")]
    [TestCase("release.rar 0x1A2B3C")]
    public async Task VerifyAsync_InvalidLine_ThrowsWithSfvNameAndLine(string line)
    {
        // Arrange
        var sfvFilePath = WriteFile("release.sfv", "release.r00 1A2B3C4D", line);

        // Act
        var verify = () => verifier.VerifyAsync(download, [sfvFilePath], CancellationToken.None);

        // Assert
        var exception = await Should.ThrowAsync<InvalidDataException>(verify);
        exception.Message.ShouldContain("release.sfv");
        exception.Message.ShouldContain($"'{line}'");
    }

    [TestCase("../outside.rar 1A2B3C4D")]
    [TestCase(@"..\outside.rar 1A2B3C4D")]
    [TestCase("CD1/../../outside.rar 1A2B3C4D")]
    [TestCase("/etc/passwd 1A2B3C4D")]
    public async Task VerifyAsync_EntryOutsideOfDownloadFolder_Throws(string line)
    {
        // Arrange
        var sfvFilePath = WriteFile("release.sfv", line);

        // Act
        var verify = () => verifier.VerifyAsync(download, [sfvFilePath], CancellationToken.None);

        // Assert
        var exception = await Should.ThrowAsync<InvalidDataException>(verify);
        exception.Message.ShouldContain("release.sfv");
        exception.Message.ShouldContain("unsafe path");
    }

    private static uint ComputeCrc32(string content)
    {
        return Crc32.HashToUInt32(Encoding.UTF8.GetBytes(content));
    }

    private static string CreateSfvLine(string relativeFilePath, string content)
    {
        return $"{relativeFilePath} {ComputeCrc32(content):X8}";
    }

    private string WriteFile(string relativePath, params string[] lines)
    {
        var filePath = Path.Combine(localFolderPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, string.Join('\n', lines));

        return filePath;
    }
}
