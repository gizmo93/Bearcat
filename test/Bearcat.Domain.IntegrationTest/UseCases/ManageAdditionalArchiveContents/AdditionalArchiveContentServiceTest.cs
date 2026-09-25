using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents;
using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Dto;
using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Validation;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageAdditionalArchiveContents;

public class AdditionalArchiveContentServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private string tempRootPath = null!;
    private string sourceFolderPath = null!;
    private string sourceFilePath = null!;
    private AdditionalArchiveContentService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        tempRootPath = Path.Combine(Path.GetTempPath(), $"bearcat-tests-{Guid.NewGuid():N}");
        sourceFolderPath = Directory.CreateDirectory(Path.Combine(tempRootPath, "ads")).FullName;
        sourceFilePath = Path.Combine(tempRootPath, "premium.txt");
        File.WriteAllText(sourceFilePath, "premium");
        service = new AdditionalArchiveContentService(
            new AdditionalArchiveContentRepository(dbContext, dbContext),
            new FileSystemService()
        );
    }

    [TearDown]
    public async Task DisposeResourcesAsync()
    {
        await dbContext.DisposeAsync();

        if (Directory.Exists(tempRootPath))
        {
            Directory.Delete(tempRootPath, recursive: true);
        }
    }

    [Test]
    public async Task CreateAsync_PathContentWithExistingFolder_PersistsTrimmedContent()
    {
        // Act
        var result = await service.CreateAsync(
            CreatePathInput("  Premium ad folder  ", $"  {sourceFolderPath}  ")
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var content = await LoadContentAsync();
        content.Id.ShouldBe(result.AdditionalArchiveContentId!.Value);
        content.Name.ShouldBe("Premium ad folder");
        content.Type.ShouldBe(AdditionalArchiveContentType.Path);
        content.SourcePath.ShouldBe(sourceFolderPath);
        content.FileName.ShouldBeNull();
        content.TextContent.ShouldBeNull();
    }

    [Test]
    public async Task CreateAsync_PathContentWithExistingFile_PersistsContent()
    {
        // Act
        var result = await service.CreateAsync(CreatePathInput("Premium ad file", sourceFilePath));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await LoadContentAsync()).SourcePath.ShouldBe(sourceFilePath);
    }

    [Test]
    public async Task CreateAsync_PathContentWithTextFileFields_PersistsOnlyPathFields()
    {
        // Act
        var result = await service.CreateAsync(
            new AdditionalArchiveContentInput(
                "Premium ad folder",
                AdditionalArchiveContentType.Path,
                sourceFolderPath,
                "ignored.txt",
                "ignored"
            )
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var content = await LoadContentAsync();
        content.FileName.ShouldBeNull();
        content.TextContent.ShouldBeNull();
    }

    [Test]
    public async Task CreateAsync_PathContentWithMissingSourcePath_ReturnsSourcePathNotFound()
    {
        // Act
        var result = await service.CreateAsync(
            CreatePathInput("Premium ad folder", Path.Combine(tempRootPath, "missing"))
        );

        // Assert
        result.ValidationErrors.ShouldBe([
            AdditionalArchiveContentValidationError.SourcePathNotFound,
        ]);
        result.AdditionalArchiveContentId.ShouldBeNull();
        (await dbContext.AdditionalArchiveContents.AnyAsync()).ShouldBeFalse();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task CreateAsync_PathContentWithoutSourcePath_ReturnsSourcePathRequired(
        string? sourcePath
    )
    {
        // Act
        var result = await service.CreateAsync(CreatePathInput("Premium ad folder", sourcePath));

        // Assert
        result.ValidationErrors.ShouldBe([
            AdditionalArchiveContentValidationError.SourcePathRequired,
        ]);
    }

    [Test]
    public async Task CreateAsync_SourcePathLongerThanMaximum_ReturnsSourcePathTooLong()
    {
        // Act
        var result = await service.CreateAsync(
            CreatePathInput("Premium ad folder", "/" + new string('a', 1000))
        );

        // Assert
        result.ValidationErrors.ShouldBe([
            AdditionalArchiveContentValidationError.SourcePathTooLong,
        ]);
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task CreateAsync_BlankName_ReturnsNameRequired(string name)
    {
        // Act
        var result = await service.CreateAsync(CreatePathInput(name, sourceFolderPath));

        // Assert
        result.ValidationErrors.ShouldBe([AdditionalArchiveContentValidationError.NameRequired]);
    }

    [Test]
    public async Task CreateAsync_NameLongerThanMaximum_ReturnsNameTooLong()
    {
        // Act
        var result = await service.CreateAsync(
            CreatePathInput(new string('a', 201), sourceFolderPath)
        );

        // Assert
        result.ValidationErrors.ShouldBe([AdditionalArchiveContentValidationError.NameTooLong]);
    }

    [Test]
    public async Task CreateAsync_NameOfExistingContentWithSurroundingWhitespace_ReturnsNameAlreadyExists()
    {
        // Arrange
        await service.CreateAsync(CreatePathInput("Premium ad", sourceFolderPath));

        // Act
        var result = await service.CreateAsync(
            CreateTextFileInput("  Premium ad  ", "premium.txt", "Buy premium.")
        );

        // Assert
        result.ValidationErrors.ShouldBe([
            AdditionalArchiveContentValidationError.NameAlreadyExists,
        ]);
        (await dbContext.AdditionalArchiveContents.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task CreateAsync_TextFileContent_PersistsTrimmedFileNameAndUnchangedTextContent()
    {
        // Act
        var result = await service.CreateAsync(
            new AdditionalArchiveContentInput(
                "Premium ad text",
                AdditionalArchiveContentType.TextFile,
                sourceFolderPath,
                "  buy premium via me.txt  ",
                "  Buy premium via my link.\n"
            )
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var content = await LoadContentAsync();
        content.Type.ShouldBe(AdditionalArchiveContentType.TextFile);
        content.SourcePath.ShouldBeNull();
        content.FileName.ShouldBe("buy premium via me.txt");
        content.TextContent.ShouldBe("  Buy premium via my link.\n");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task CreateAsync_TextFileContentWithoutFileName_ReturnsFileNameRequired(
        string? fileName
    )
    {
        // Act
        var result = await service.CreateAsync(
            CreateTextFileInput("Premium ad text", fileName, "Buy premium.")
        );

        // Assert
        result.ValidationErrors.ShouldBe([
            AdditionalArchiveContentValidationError.FileNameRequired,
        ]);
    }

    [TestCase("ads/premium.txt")]
    [TestCase("ads\\premium.txt")]
    [TestCase(".")]
    [TestCase("..")]
    [TestCase("premium\0.txt")]
    public async Task CreateAsync_TextFileContentWithInvalidFileName_ReturnsFileNameInvalid(
        string fileName
    )
    {
        // Act
        var result = await service.CreateAsync(
            CreateTextFileInput("Premium ad text", fileName, "Buy premium.")
        );

        // Assert
        result.ValidationErrors.ShouldBe([AdditionalArchiveContentValidationError.FileNameInvalid]);
    }

    [Test]
    public async Task CreateAsync_FileNameLongerThanMaximum_ReturnsFileNameTooLong()
    {
        // Act
        var result = await service.CreateAsync(
            CreateTextFileInput("Premium ad text", new string('a', 256), "Buy premium.")
        );

        // Assert
        result.ValidationErrors.ShouldBe([AdditionalArchiveContentValidationError.FileNameTooLong]);
    }

    [TestCase("__nonce.txt")]
    [TestCase("__NONCE.TXT")]
    public async Task CreateAsync_TextFileContentWithNonceFileName_ReturnsFileNameReserved(
        string fileName
    )
    {
        // Act
        var result = await service.CreateAsync(
            CreateTextFileInput("Premium ad text", fileName, "Buy premium.")
        );

        // Assert
        result.ValidationErrors.ShouldBe([
            AdditionalArchiveContentValidationError.FileNameReserved,
        ]);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" \n ")]
    public async Task CreateAsync_TextFileContentWithoutTextContent_ReturnsTextContentRequired(
        string? textContent
    )
    {
        // Act
        var result = await service.CreateAsync(
            CreateTextFileInput("Premium ad text", "premium.txt", textContent)
        );

        // Assert
        result.ValidationErrors.ShouldBe([
            AdditionalArchiveContentValidationError.TextContentRequired,
        ]);
    }

    [Test]
    public async Task CreateAsync_SeveralInvalidFields_ReturnsAllValidationErrors()
    {
        // Act
        var result = await service.CreateAsync(CreateTextFileInput(" ", "..", " "));

        // Assert
        result.ValidationErrors.ShouldBe([
            AdditionalArchiveContentValidationError.NameRequired,
            AdditionalArchiveContentValidationError.FileNameInvalid,
            AdditionalArchiveContentValidationError.TextContentRequired,
        ]);
    }

    [Test]
    public async Task UpdateAsync_UnchangedNameOfSameContent_UpdatesContent()
    {
        // Arrange
        var created = await service.CreateAsync(CreatePathInput("Premium ad", sourceFolderPath));
        var id = created.AdditionalArchiveContentId!.Value;

        // Act
        var result = await service.UpdateAsync(id, CreatePathInput("Premium ad", sourceFilePath));

        // Assert
        result.ShouldBe(AdditionalArchiveContentSaveResult.Saved(id));
        (await LoadContentAsync()).SourcePath.ShouldBe(sourceFilePath);
    }

    [Test]
    public async Task UpdateAsync_NameOfOtherContent_ReturnsNameAlreadyExistsAndKeepsContent()
    {
        // Arrange
        await service.CreateAsync(CreatePathInput("Premium ad", sourceFolderPath));
        var created = await service.CreateAsync(CreatePathInput("Other ad", sourceFolderPath));
        var id = created.AdditionalArchiveContentId!.Value;

        // Act
        var result = await service.UpdateAsync(id, CreatePathInput("Premium ad", sourceFilePath));

        // Assert
        result.ValidationErrors.ShouldBe([
            AdditionalArchiveContentValidationError.NameAlreadyExists,
        ]);
        dbContext.ChangeTracker.Clear();
        var content = await dbContext.AdditionalArchiveContents.SingleAsync(content =>
            content.Id == id
        );
        content.Name.ShouldBe("Other ad");
        content.SourcePath.ShouldBe(sourceFolderPath);
    }

    [Test]
    public async Task UpdateAsync_PathContentChangedToTextFile_ClearsSourcePath()
    {
        // Arrange
        var created = await service.CreateAsync(CreatePathInput("Premium ad", sourceFolderPath));
        var id = created.AdditionalArchiveContentId!.Value;

        // Act
        var result = await service.UpdateAsync(
            id,
            new AdditionalArchiveContentInput(
                "Premium ad",
                AdditionalArchiveContentType.TextFile,
                sourceFolderPath,
                "premium.txt",
                "Buy premium."
            )
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var content = await LoadContentAsync();
        content.Type.ShouldBe(AdditionalArchiveContentType.TextFile);
        content.SourcePath.ShouldBeNull();
        content.FileName.ShouldBe("premium.txt");
        content.TextContent.ShouldBe("Buy premium.");
    }

    [Test]
    public async Task UpdateAsync_TextFileContentChangedToPath_ClearsFileNameAndTextContent()
    {
        // Arrange
        var created = await service.CreateAsync(
            CreateTextFileInput("Premium ad", "premium.txt", "Buy premium.")
        );
        var id = created.AdditionalArchiveContentId!.Value;

        // Act
        var result = await service.UpdateAsync(
            id,
            new AdditionalArchiveContentInput(
                "Premium ad",
                AdditionalArchiveContentType.Path,
                sourceFolderPath,
                "premium.txt",
                "Buy premium."
            )
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var content = await LoadContentAsync();
        content.Type.ShouldBe(AdditionalArchiveContentType.Path);
        content.SourcePath.ShouldBe(sourceFolderPath);
        content.FileName.ShouldBeNull();
        content.TextContent.ShouldBeNull();
    }

    [Test]
    public async Task DeleteAsync_UnassignedContent_DeletesContent()
    {
        // Arrange
        var created = await service.CreateAsync(CreatePathInput("Premium ad", sourceFolderPath));

        // Act
        var result = await service.DeleteAsync(created.AdditionalArchiveContentId!.Value);

        // Assert
        result.IsDeleted.ShouldBeTrue();
        result.UsingReleaseTemplateNames.ShouldBeEmpty();
        result.UsingReleaseNames.ShouldBeEmpty();
        (await dbContext.AdditionalArchiveContents.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task DeleteAsync_ContentAssignedToTemplateAndArchiveConfig_KeepsContentAndReturnsUsage()
    {
        // Arrange
        var created = await service.CreateAsync(CreatePathInput("Premium ad", sourceFolderPath));
        var id = created.AdditionalArchiveContentId!.Value;
        await AssignToReleaseTemplateAndReleaseAsync(id);

        // Act
        var result = await service.DeleteAsync(id);

        // Assert
        result.IsDeleted.ShouldBeFalse();
        result.UsingReleaseTemplateNames.ShouldBe(["Managed template"]);
        result.UsingReleaseNames.ShouldBe(["Bearcat.Release.001"]);
        dbContext.ChangeTracker.Clear();
        (await dbContext.AdditionalArchiveContents.CountAsync()).ShouldBe(1);
    }

    private static AdditionalArchiveContentInput CreatePathInput(string name, string? sourcePath)
    {
        return new AdditionalArchiveContentInput(
            name,
            AdditionalArchiveContentType.Path,
            sourcePath,
            FileName: null,
            TextContent: null
        );
    }

    private static AdditionalArchiveContentInput CreateTextFileInput(
        string name,
        string? fileName,
        string? textContent
    )
    {
        return new AdditionalArchiveContentInput(
            name,
            AdditionalArchiveContentType.TextFile,
            SourcePath: null,
            fileName,
            textContent
        );
    }

    private async Task<AdditionalArchiveContent> LoadContentAsync()
    {
        await using var readDbContext = Database.CreateDbContext();

        return await readDbContext.AdditionalArchiveContents.SingleAsync();
    }

    private async Task AssignToReleaseTemplateAndReleaseAsync(int additionalArchiveContentId)
    {
        await using var setupDbContext = Database.CreateDbContext();
        var content = await setupDbContext.AdditionalArchiveContents.SingleAsync(content =>
            content.Id == additionalArchiveContentId
        );
        var releaseGroup = new ReleaseGroup
        {
            Name = "Managed releases",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };
        setupDbContext.ReleaseTemplates.Add(
            new ReleaseTemplate
            {
                Name = "Managed template",
                ReleaseType = ReleaseType.Managed,
                ReleaseGroup = releaseGroup,
                ArchiveConfigTemplates =
                [
                    new ArchiveConfigTemplate
                    {
                        Name = "RAR Forum A",
                        ArchiveFilesBasePath = "/tmp/archives",
                        ArchiverName = "rar",
                        ArchiveFileSizeMb = 1024,
                        AdditionalArchiveContents = [content],
                    },
                ],
            }
        );
        setupDbContext.Releases.Add(
            new Release
            {
                Name = "Bearcat.Release.001",
                CreatedAt = DateTime.UtcNow,
                ReleaseType = ReleaseType.Managed,
                ReleaseFolderPath = "/tmp/releases/Bearcat.Release.001",
                ReleaseGroup = releaseGroup,
                ArchiveConfigs =
                [
                    new ArchiveConfig
                    {
                        Name = "RAR Forum A",
                        ArchiveFilesBasePath = "/tmp/archives",
                        ArchiverName = "rar",
                        ArchiveFileSizeMb = 1024,
                        AdditionalArchiveContents = [content],
                    },
                ],
            }
        );
        await setupDbContext.SaveChangesAsync();
    }
}
