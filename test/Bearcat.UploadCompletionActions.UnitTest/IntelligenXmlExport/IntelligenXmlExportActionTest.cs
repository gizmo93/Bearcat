using System.Xml.Linq;
using Bearcat.Abstractions.UploadCompletionAction.Dto;
using Bearcat.UploadCompletionActions.IntelligenXmlExport;
using Shouldly;

namespace Bearcat.UploadCompletionActions.UnitTest.IntelligenXmlExport;

public class IntelligenXmlExportActionTest
{
    private readonly IntelligenXmlExportAction action = new();

    private string temporaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        temporaryDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(temporaryDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(temporaryDirectory, recursive: true);
    }

    [Test]
    public async Task ExecuteAsync_TargetFileExists_WritesNumberedFileInSubfolder()
    {
        // Arrange
        var exportDirectory = Path.Combine(temporaryDirectory, "export");
        Directory.CreateDirectory(Path.Combine(exportDirectory, "Bearcat"));
        var existingFilePath = Path.Combine(exportDirectory, "Bearcat", "Bearcat.Release.xml");
        await File.WriteAllTextAsync(existingFilePath, "existing");

        var config = new IntelligenXmlExportConfig
        {
            ExportDirectory = exportDirectory,
            RelativeFilePathTemplate = "Bearcat/{{ release.name }}.xml",
        };

        // Act
        var filePath = await action.ExecuteAsync(config, CreateInput(), CancellationToken.None);

        // Assert
        filePath.ShouldBe(Path.Combine(exportDirectory, "Bearcat", "Bearcat.Release (2).xml"));
        (await File.ReadAllTextAsync(existingFilePath)).ShouldBe("existing");
        XDocument.Load(filePath).Root!.Element("templatetype")!.Value.ShouldBe("Other");
    }

    [Test]
    public async Task ExecuteAsync_RelativePathEscapesExportDirectory_ThrowsAndWritesNothing()
    {
        // Arrange
        var exportDirectory = Path.Combine(temporaryDirectory, "export");
        Directory.CreateDirectory(exportDirectory);

        var config = new IntelligenXmlExportConfig
        {
            ExportDirectory = exportDirectory,
            RelativeFilePathTemplate = "../{{ release.name }}.xml",
        };

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            action.ExecuteAsync(config, CreateInput(), CancellationToken.None)
        );

        // Assert
        exception.Message.ShouldContain("outside of the export directory");
        Directory.GetFiles(temporaryDirectory, "*", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Test]
    public async Task ExecuteAsync_ExportDirectoryMissing_ThrowsAndDoesNotCreateIt()
    {
        // Arrange
        var exportDirectory = Path.Combine(temporaryDirectory, "missing");

        var config = new IntelligenXmlExportConfig { ExportDirectory = exportDirectory };

        // Act
        await Should.ThrowAsync<DirectoryNotFoundException>(() =>
            action.ExecuteAsync(config, CreateInput(), CancellationToken.None)
        );

        // Assert
        Directory.Exists(exportDirectory).ShouldBeFalse();
    }

    private static UploadCompletionActionInput CreateInput()
    {
        return new UploadCompletionActionInput(
            Release: new UploadCompletionActionReleaseDto(
                Name: "Bearcat.Release",
                ContentType: null,
                Platform: null,
                Title: null,
                Genre: null,
                Description: null,
                RuntimeMinutes: null,
                VideoType: null,
                VideoCodec: null,
                AudioType: null,
                AudioCodec: null,
                AudioChannels: null,
                AudioLanguages: [],
                Nfo: null
            ),
            CollectionReleaseNames: [],
            IsReupload: false,
            Mirrors: [],
            AllHostersLinkCrypterContainers: [],
            Images: []
        );
    }
}
