using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageAdditionalArchiveContents;

public class AdditionalArchiveContentRepositoryTest : BearcatIntegrationTest
{
    [Test]
    public async Task GetDetailAsync_AddedPathAndTextFileContents_ReturnsAllProperties()
    {
        // Arrange
        var repository = CreateRepository();
        var pathContent = CreatePathContent("Premium ad folder");
        var textFileContent = CreateTextFileContent("Premium ad text");
        repository.Add(pathContent);
        repository.Add(textFileContent);
        await repository.SaveChangesAsync(CancellationToken.None);
        var readRepository = CreateRepository();

        // Act
        var pathDetail = await readRepository.GetDetailReadModelAsync(pathContent.Id);
        var textFileDetail = await readRepository.GetDetailReadModelAsync(textFileContent.Id);

        // Assert
        pathDetail.ShouldBe(
            new AdditionalArchiveContentDetailReadModel(
                pathContent.Id,
                "Premium ad folder",
                AdditionalArchiveContentType.Path,
                "/data/ads/premium",
                FileName: null,
                TextContent: null
            )
        );
        textFileDetail.ShouldBe(
            new AdditionalArchiveContentDetailReadModel(
                textFileContent.Id,
                "Premium ad text",
                AdditionalArchiveContentType.TextFile,
                SourcePath: null,
                "buy premium via me.txt",
                "Buy premium via my link."
            )
        );
    }

    [Test]
    public async Task GetDetailAsync_UnknownId_ReturnsNull()
    {
        // Act
        var result = await CreateRepository().GetDetailReadModelAsync(4711);

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task GetAllAsync_ContentsWithAssignments_ReturnsContentsOrderedByNameWithUsageCounts()
    {
        // Arrange
        var dbContext = CreateDbContext();
        var assignedContent = CreatePathContent("Premium ad folder");
        var unassignedContent = CreateTextFileContent("Another premium ad");
        dbContext.AdditionalArchiveContents.AddRange(assignedContent, unassignedContent);
        await dbContext.SaveChangesAsync();
        await AddReleaseTemplateWithArchiveConfigTemplatesAsync(
            "Managed template",
            [assignedContent.Id],
            [assignedContent.Id]
        );
        await AddReleaseWithArchiveConfigAsync("Bearcat.Release.001", assignedContent.Id);

        // Act
        var result = await CreateRepository().GetAllAsync();

        // Assert
        result.ShouldBe([
            new AdditionalArchiveContentReadModel(
                unassignedContent.Id,
                "Another premium ad",
                AdditionalArchiveContentType.TextFile,
                SourcePath: null,
                "buy premium via me.txt",
                ArchiveConfigTemplateCount: 0,
                ArchiveConfigCount: 0
            ),
            new AdditionalArchiveContentReadModel(
                assignedContent.Id,
                "Premium ad folder",
                AdditionalArchiveContentType.Path,
                "/data/ads/premium",
                FileName: null,
                ArchiveConfigTemplateCount: 2,
                ArchiveConfigCount: 1
            ),
        ]);
    }

    [Test]
    public async Task NameExistsAsync_NameOfExistingContent_ReturnsTrue()
    {
        // Arrange
        await AddContentAsync(CreatePathContent("Premium ad"));

        // Act
        var result = await CreateRepository()
            .NameExistsAsync(
                "Premium ad",
                excludedAdditionalArchiveContentId: null,
                CancellationToken.None
            );

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public async Task NameExistsAsync_NameOnlyUsedByExcludedContent_ReturnsFalse()
    {
        // Arrange
        var content = await AddContentAsync(CreatePathContent("Premium ad"));

        // Act
        var result = await CreateRepository()
            .NameExistsAsync("Premium ad", content.Id, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public async Task NameExistsAsync_UnusedName_ReturnsFalse()
    {
        // Arrange
        await AddContentAsync(CreatePathContent("Premium ad"));

        // Act
        var result = await CreateRepository()
            .NameExistsAsync(
                "Other ad",
                excludedAdditionalArchiveContentId: null,
                CancellationToken.None
            );

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public async Task GetUsageAsync_ContentAssignedToTemplatesAndArchiveConfigs_ReturnsDistinctSortedNames()
    {
        // Arrange
        var content = await AddContentAsync(CreatePathContent("Premium ad folder"));
        await AddReleaseTemplateWithArchiveConfigTemplatesAsync(
            "Series template",
            [content.Id],
            [content.Id]
        );
        await AddReleaseTemplateWithArchiveConfigTemplatesAsync("Movie template", [content.Id]);
        await AddReleaseWithArchiveConfigAsync("Bearcat.Release.002", content.Id);
        await AddReleaseWithArchiveConfigAsync("Bearcat.Release.001", content.Id);

        // Act
        var result = await CreateRepository().GetUsageAsync(content.Id, CancellationToken.None);

        // Assert
        result.ReleaseTemplateNames.ShouldBe(["Movie template", "Series template"]);
        result.ReleaseNames.ShouldBe(["Bearcat.Release.001", "Bearcat.Release.002"]);
        result.IsUsed.ShouldBeTrue();
    }

    [Test]
    public async Task GetUsageAsync_AssignedReleaseWasDeleted_ReturnsNoUsageAndKeepsContent()
    {
        // Arrange
        var content = await AddContentAsync(CreatePathContent("Premium ad folder"));
        await AddReleaseWithArchiveConfigAsync("Bearcat.Release.001", content.Id);
        var deletionDbContext = CreateDbContext();
        deletionDbContext.Releases.Remove(await deletionDbContext.Releases.SingleAsync());
        await deletionDbContext.SaveChangesAsync();

        // Act
        var result = await CreateRepository().GetUsageAsync(content.Id, CancellationToken.None);

        // Assert
        result.IsUsed.ShouldBeFalse();
        (await CreateRepository().GetDetailReadModelAsync(content.Id)).ShouldNotBeNull();
    }

    [Test]
    public async Task Remove_UnassignedContent_DeletesContent()
    {
        // Arrange
        var content = await AddContentAsync(CreatePathContent("Premium ad folder"));
        var repository = CreateRepository();
        repository.Remove(await repository.GetByIdAsync(content.Id, CancellationToken.None));

        // Act
        await repository.SaveChangesAsync(CancellationToken.None);

        // Assert
        (await CreateRepository().GetAllAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task Remove_ContentAssignedToArchiveConfigTemplate_ThrowsDbUpdateException()
    {
        // Arrange
        var content = await AddContentAsync(CreatePathContent("Premium ad folder"));
        await AddReleaseTemplateWithArchiveConfigTemplatesAsync("Managed template", [content.Id]);
        var repository = CreateRepository();
        repository.Remove(await repository.GetByIdAsync(content.Id, CancellationToken.None));

        // Act
        var result = await Should.ThrowAsync<DbUpdateException>(() =>
            repository.SaveChangesAsync(CancellationToken.None)
        );

        // Assert
        result.ShouldNotBeNull();
        (await CreateRepository().GetAllAsync()).Count.ShouldBe(1);
    }

    [Test]
    public async Task Remove_ContentAssignedToArchiveConfig_ThrowsDbUpdateException()
    {
        // Arrange
        var content = await AddContentAsync(CreateTextFileContent("Premium ad text"));
        await AddReleaseWithArchiveConfigAsync("Bearcat.Release.001", content.Id);
        var repository = CreateRepository();
        repository.Remove(await repository.GetByIdAsync(content.Id, CancellationToken.None));

        // Act
        var result = await Should.ThrowAsync<DbUpdateException>(() =>
            repository.SaveChangesAsync(CancellationToken.None)
        );

        // Assert
        result.ShouldNotBeNull();
        (await CreateRepository().GetAllAsync()).Count.ShouldBe(1);
    }

    [Test]
    public async Task SaveChangesAsync_PathContentWithFileName_ThrowsCheckConstraintViolation()
    {
        // Arrange
        var repository = CreateRepository();
        var content = CreatePathContent("Premium ad folder");
        content.FileName = "buy premium via me.txt";
        repository.Add(content);

        // Act
        var result = await Should.ThrowAsync<DbUpdateException>(() =>
            repository.SaveChangesAsync(CancellationToken.None)
        );

        // Assert
        result
            .InnerException.ShouldBeOfType<PostgresException>()
            .ConstraintName.ShouldBe("CK_AdditionalArchiveContent_FieldsMatchType");
    }

    [Test]
    public async Task SaveChangesAsync_TextFileContentWithoutTextContent_ThrowsCheckConstraintViolation()
    {
        // Arrange
        var repository = CreateRepository();
        var content = CreateTextFileContent("Premium ad text");
        content.TextContent = null;
        repository.Add(content);

        // Act
        var result = await Should.ThrowAsync<DbUpdateException>(() =>
            repository.SaveChangesAsync(CancellationToken.None)
        );

        // Assert
        result
            .InnerException.ShouldBeOfType<PostgresException>()
            .ConstraintName.ShouldBe("CK_AdditionalArchiveContent_FieldsMatchType");
    }

    [Test]
    public async Task SaveChangesAsync_DuplicateName_ThrowsDbUpdateException()
    {
        // Arrange
        await AddContentAsync(CreatePathContent("Premium ad"));
        var repository = CreateRepository();
        repository.Add(CreateTextFileContent("Premium ad"));

        // Act
        var result = await Should.ThrowAsync<DbUpdateException>(() =>
            repository.SaveChangesAsync(CancellationToken.None)
        );

        // Assert
        result.ShouldNotBeNull();
    }

    private AdditionalArchiveContentRepository CreateRepository()
    {
        var dbContext = CreateDbContext();

        return new AdditionalArchiveContentRepository(dbContext, dbContext);
    }

    private static AdditionalArchiveContent CreatePathContent(string name)
    {
        return new AdditionalArchiveContent
        {
            Name = name,
            Type = AdditionalArchiveContentType.Path,
            SourcePath = "/data/ads/premium",
        };
    }

    private static AdditionalArchiveContent CreateTextFileContent(string name)
    {
        return new AdditionalArchiveContent
        {
            Name = name,
            Type = AdditionalArchiveContentType.TextFile,
            FileName = "buy premium via me.txt",
            TextContent = "Buy premium via my link.",
        };
    }

    private async Task<AdditionalArchiveContent> AddContentAsync(AdditionalArchiveContent content)
    {
        var dbContext = CreateDbContext();
        dbContext.AdditionalArchiveContents.Add(content);
        await dbContext.SaveChangesAsync();

        return content;
    }

    private async Task AddReleaseTemplateWithArchiveConfigTemplatesAsync(
        string releaseTemplateName,
        params int[][] additionalArchiveContentIdsPerArchiveConfigTemplate
    )
    {
        var releaseGroup = await AddReleaseGroupAsync();
        var dbContext = CreateDbContext();
        var archiveConfigTemplates = new List<ArchiveConfigTemplate>();

        foreach (
            var additionalArchiveContentIds in additionalArchiveContentIdsPerArchiveConfigTemplate
        )
        {
            archiveConfigTemplates.Add(
                new ArchiveConfigTemplate
                {
                    Name = $"RAR Forum {archiveConfigTemplates.Count + 1}",
                    ArchiveFilesBasePath = "/tmp/archives",
                    ArchiverName = "rar",
                    ArchiveFileSizeMb = 1024,
                    AdditionalArchiveContents = await dbContext
                        .AdditionalArchiveContents.Where(content =>
                            additionalArchiveContentIds.Contains(content.Id)
                        )
                        .ToListAsync(),
                }
            );
        }

        dbContext.ReleaseTemplates.Add(
            new ReleaseTemplate
            {
                Name = releaseTemplateName,
                ReleaseType = ReleaseType.Managed,
                ReleaseGroupId = releaseGroup.Id,
                ArchiveConfigTemplates = archiveConfigTemplates,
            }
        );
        await dbContext.SaveChangesAsync();
    }

    private async Task AddReleaseWithArchiveConfigAsync(
        string releaseName,
        int additionalArchiveContentId
    )
    {
        var releaseGroup = await AddReleaseGroupAsync();
        var dbContext = CreateDbContext();
        dbContext.Releases.Add(
            new Release
            {
                Name = releaseName,
                CreatedAt = DateTime.UtcNow,
                ReleaseType = ReleaseType.Managed,
                ReleaseFolderPath = $"/tmp/releases/{releaseName}",
                ReleaseGroupId = releaseGroup.Id,
                ArchiveConfigs =
                [
                    new ArchiveConfig
                    {
                        Name = "RAR Forum A",
                        ArchiveFilesBasePath = "/tmp/archives",
                        ArchiverName = "rar",
                        ArchiveFileSizeMb = 1024,
                        AdditionalArchiveContents =
                        [
                            await dbContext.AdditionalArchiveContents.SingleAsync(content =>
                                content.Id == additionalArchiveContentId
                            ),
                        ],
                    },
                ],
            }
        );
        await dbContext.SaveChangesAsync();
    }

    private async Task<ReleaseGroup> AddReleaseGroupAsync()
    {
        var dbContext = CreateDbContext();
        var releaseGroup = new ReleaseGroup
        {
            Name = $"Managed releases {Guid.NewGuid():N}",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };

        dbContext.ReleaseGroups.Add(releaseGroup);
        await dbContext.SaveChangesAsync();

        return releaseGroup;
    }
}
