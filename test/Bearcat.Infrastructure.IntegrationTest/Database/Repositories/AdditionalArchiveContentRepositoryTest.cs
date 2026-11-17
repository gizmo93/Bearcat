using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class AdditionalArchiveContentRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
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
