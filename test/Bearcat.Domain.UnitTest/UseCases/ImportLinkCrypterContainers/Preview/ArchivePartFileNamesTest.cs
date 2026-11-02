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
}
