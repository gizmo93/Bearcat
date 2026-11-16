using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ImportLinkCrypterContainers.Preview;

public class ArchivePartFileNamesTest
{
    [TestCase("Release.rar", ".rar")]
    [TestCase("Release.part01.rar", ".rar")]
    [TestCase("Release.R00", ".rar")]
    [TestCase("Release.r123", ".rar")]
    [TestCase("Release.7z", ".7z")]
    [TestCase("Release.7Z.001", ".7z")]
    [TestCase("Release.mkv", null)]
    [TestCase("Release.zip", null)]
    [TestCase("Release.r1", null)]
    [TestCase("Release.7z.01", null)]
    [TestCase(".rar", null)]
    public void GetArchiverFileExtension_FileName_ReturnsExtensionOfArchiverForArchiveParts(
        string fileName,
        string? expectedArchiverFileExtension
    )
    {
        // Act
        var archiverFileExtension = ArchivePartFileNames.GetArchiverFileExtension(fileName);

        // Assert
        archiverFileExtension.ShouldBe(expectedArchiverFileExtension);
    }

    [TestCase(
        "Show.S01E01.German.1080p.WEB.x264-GRP.part1.rar",
        "Show.S01E01.German.1080p.WEB.x264-GRP"
    )]
    [TestCase("Show.S01E02.PART12.RAR", "Show.S01E02")]
    [TestCase("Release.rar", "Release")]
    [TestCase("Release.r00", "Release")]
    [TestCase("Release.7z", "Release")]
    [TestCase("Release.7z.001", "Release")]
    [TestCase("Release.mkv", null)]
    public void GetArchiveBaseName_FileName_ReturnsNameWithoutPartSuffixForArchiveParts(
        string fileName,
        string? expectedArchiveBaseName
    )
    {
        // Act
        var archiveBaseName = ArchivePartFileNames.GetArchiveBaseName(fileName);

        // Assert
        archiveBaseName.ShouldBe(expectedArchiveBaseName);
    }
}
