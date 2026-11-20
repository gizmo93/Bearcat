using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Dto;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class LinkCrypterImportReadRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime CreatedAt = new(
        2026,
        11,
        2,
        12,
        0,
        0,
        DateTimeKind.Unspecified
    );

    [Test]
    public async Task SearchReleasesAsync_StatusFilterAndPaging_ReturnsReleasesWithAllTheirContainersByLowestContainerId()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var linkCrypterImport = AddImport(dbContext);
        AddContainer(linkCrypterImport, "Release.A", LinkCrypterImportContainerStatus.Importable);
        AddContainer(
            linkCrypterImport,
            "Release.B",
            LinkCrypterImportContainerStatus.NotImportable
        );
        AddContainer(
            linkCrypterImport,
            "release.a",
            LinkCrypterImportContainerStatus.Importable,
            isSelected: false
        );
        AddContainer(
            linkCrypterImport,
            "Release.C",
            LinkCrypterImportContainerStatus.AlreadyInBearcat
        );
        AddContainer(linkCrypterImport, "Release.D", LinkCrypterImportContainerStatus.Importable);
        AddContainer(linkCrypterImport, "Release.E", LinkCrypterImportContainerStatus.Importable);
        AddContainer(linkCrypterImport, "Release.F", LinkCrypterImportContainerStatus.Importable);
        AddContainer(linkCrypterImport, "Release.G", LinkCrypterImportContainerStatus.Importable);
        AddContainer(
            linkCrypterImport,
            "Release.B",
            LinkCrypterImportContainerStatus.AlreadyInBearcat
        );
        var otherImport = AddImport(dbContext);
        AddContainer(otherImport, "Release.H", LinkCrypterImportContainerStatus.Importable);
        await dbContext.SaveChangesAsync();
        var repository = new LinkCrypterImportReadRepository(CreateDbContext());

        // Act
        var result = await repository.SearchReleasesAsync(
            new LinkCrypterImportReleaseSearchQuery(
                Filter: new LinkCrypterImportReleaseFilter(
                    LinkCrypterImportId: linkCrypterImport.Id,
                    Statuses:
                    [
                        LinkCrypterImportContainerStatus.Importable,
                        LinkCrypterImportContainerStatus.NotImportable,
                    ],
                    SearchText: null
                ),
                PageIndex: 0,
                PageSize: 5
            )
        );

        // Assert
        result.TotalCount.ShouldBe(6);
        result
            .Items.Select(release => release.ReleaseName)
            .ShouldBe(["Release.A", "Release.B", "Release.D", "Release.E", "Release.F"]);
        var releaseA = result.Items[0];
        releaseA
            .Containers.Select(container => container.ContainerName)
            .ShouldBe(["Release.A", "release.a"]);
        releaseA.IsSelected.ShouldBeFalse();
        releaseA.HasSelectableContainers.ShouldBeTrue();
        releaseA.ReleaseFolderPath.ShouldBe("/releases/Release.A");
        releaseA.ReleaseTemplateName.ShouldBe(linkCrypterImport.ReleaseTemplate.Name);
        var releaseB = result.Items[1];
        releaseB
            .Containers.Select(container => container.Status)
            .ShouldBe([
                LinkCrypterImportContainerStatus.NotImportable,
                LinkCrypterImportContainerStatus.AlreadyInBearcat,
            ]);
        releaseB.HasSelectableContainers.ShouldBeTrue();
        releaseB.IsSelected.ShouldBeFalse();
        result.Items[2].IsSelected.ShouldBeTrue();
    }

    [TestCase("movie", new[] { "Bearcat.Movie.2026-GRP" })]
    [TestCase("BEARCAT*grp", new[] { "Bearcat.Movie.2026-GRP" })]
    [TestCase("movie*", new string[0])]
    [TestCase("100%", new[] { "Other.Show.S01-GRP" })]
    [TestCase("report_", new[] { "Report_2026-GRP" })]
    [TestCase("pack", new[] { "Other.Show.S01-GRP", "Folder 1000 Pack" })]
    public async Task SearchReleasesAsync_SearchText_MatchesReleaseOrContainerNameWithStarAsOnlyWildcard(
        string searchText,
        string[] expectedReleaseNames
    )
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var linkCrypterImport = AddImport(dbContext);
        AddContainer(
            linkCrypterImport,
            "Bearcat.Movie.2026-GRP",
            LinkCrypterImportContainerStatus.Importable
        );
        AddContainer(
            linkCrypterImport,
            "Folder 100% Pack",
            LinkCrypterImportContainerStatus.Importable,
            releaseName: "Other.Show.S01-GRP"
        );
        AddContainer(
            linkCrypterImport,
            "Report_2026-GRP",
            LinkCrypterImportContainerStatus.Importable
        );
        AddContainer(
            linkCrypterImport,
            "Report.2026-GRP",
            LinkCrypterImportContainerStatus.Importable
        );
        AddContainer(
            linkCrypterImport,
            "Folder 1000 Pack",
            LinkCrypterImportContainerStatus.Importable
        );
        await dbContext.SaveChangesAsync();
        var repository = new LinkCrypterImportReadRepository(CreateDbContext());

        // Act
        var result = await repository.SearchReleasesAsync(
            new LinkCrypterImportReleaseSearchQuery(
                Filter: new LinkCrypterImportReleaseFilter(
                    LinkCrypterImportId: linkCrypterImport.Id,
                    Statuses: [],
                    SearchText: searchText
                ),
                PageIndex: 0,
                PageSize: 25
            )
        );

        // Assert
        result.Items.Select(release => release.ReleaseName).ShouldBe(expectedReleaseNames);
        result.TotalCount.ShouldBe(expectedReleaseNames.Length);
    }

    [Test]
    public async Task SearchReleasesAsync_SearchTextAndStatusFilter_ReturnsReleasesWithContainerMatchingBoth()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var linkCrypterImport = AddEpisodeImport(dbContext);
        await dbContext.SaveChangesAsync();
        var repository = new LinkCrypterImportReadRepository(CreateDbContext());

        // Act
        var result = await repository.SearchReleasesAsync(
            new LinkCrypterImportReleaseSearchQuery(
                Filter: new LinkCrypterImportReleaseFilter(
                    LinkCrypterImportId: linkCrypterImport.Id,
                    Statuses: [LinkCrypterImportContainerStatus.Importable],
                    SearchText: "show"
                ),
                PageIndex: 0,
                PageSize: 25
            )
        );

        // Assert
        result
            .Items.Select(release => release.ReleaseName)
            .ShouldBe(["Show.S01E01-GRP", "Show.S01E02-GRP"]);
    }

    [Test]
    public async Task GetSelectableContainersOfFilteredReleasesAsync_FilterMatchesNotImportableContainer_ReturnsImportableAndNotImportableContainersOfMatchingReleases()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var linkCrypterImport = AddEpisodeImport(dbContext);
        await dbContext.SaveChangesAsync();
        var repository = new LinkCrypterImportReadRepository(CreateDbContext());

        // Act
        var result = await repository.GetSelectableContainersOfFilteredReleasesAsync(
            new LinkCrypterImportReleaseFilter(
                LinkCrypterImportId: linkCrypterImport.Id,
                Statuses: [LinkCrypterImportContainerStatus.NotImportable],
                SearchText: "backup"
            )
        );

        // Assert
        result
            .Select(container => (container.Id, container.ReleaseName, container.IsSelected))
            .ShouldBe([
                (linkCrypterImport.Containers[0].Id, "Show.S01E01-GRP", true),
                (linkCrypterImport.Containers[1].Id, "Show.S01E01-GRP", false),
            ]);
    }

    [Test]
    public async Task GetDetailAsync_ImportWithHosterChoice_ReturnsCountsPerStatusAndSelectedImportableReleasesAndAccountsOfHoster()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var linkCrypterImport = AddImport(dbContext);
        AddContainer(linkCrypterImport, "Alpha", LinkCrypterImportContainerStatus.Importable);
        AddContainer(linkCrypterImport, "alpha", LinkCrypterImportContainerStatus.Importable);
        AddContainer(linkCrypterImport, "Bravo", LinkCrypterImportContainerStatus.Importable);
        AddContainer(
            linkCrypterImport,
            "Charlie",
            LinkCrypterImportContainerStatus.NotImportable,
            isSelected: true
        );
        var chosenAccount = CreateHosterRegistration("Chosen account", "FirstHoster");
        dbContext.HosterRegistrations.AddRange(
            chosenAccount,
            CreateHosterRegistration("Other account", "FirstHoster"),
            CreateHosterRegistration("Unrelated account", "SecondHoster")
        );
        linkCrypterImport.HosterRegistrations.Add(
            new LinkCrypterImportHosterRegistration
            {
                HosterClassName = "FirstHoster",
                HosterRegistration = chosenAccount,
            }
        );
        await dbContext.SaveChangesAsync();
        var repository = new LinkCrypterImportReadRepository(CreateDbContext());

        // Act
        var result = await repository.GetDetailAsync(linkCrypterImport.Id);

        // Assert
        result.ShouldNotBeNull();
        result
            .ContainerCountsByStatus.Select(count => (count.Status, count.Count))
            .ShouldBe([
                (LinkCrypterImportContainerStatus.Importable, 3),
                (LinkCrypterImportContainerStatus.NotImportable, 1),
            ]);
        result.SelectedImportableContainerCount.ShouldBe(3);
        result.ReleaseCountWithSelectedImportableContainers.ShouldBe(2);
        var hosterRegistration = result.HosterRegistrations.ShouldHaveSingleItem();
        hosterRegistration.HosterRegistrationId.ShouldBe(chosenAccount.Id);
        hosterRegistration
            .Options.Select(option => option.Name)
            .ShouldBe(["Chosen account", "Other account"]);
    }

    private static LinkCrypterImport AddImport(BearcatDbContext dbContext)
    {
        var linkCrypterImport = new LinkCrypterImport
        {
            LinkCrypterRegistration = new LinkCrypterRegistration
            {
                Name = $"Crypter {Guid.NewGuid():N}",
                LinkCrypterClassName = "TestCrypter",
                SerializedConfig = "{}",
                IsActive = true,
            },
            ReleaseTemplate = new ReleaseTemplate
            {
                Name = $"Template {Guid.NewGuid():N}",
                ReleaseType = ReleaseType.Unmanaged,
                ReleaseContentType = ReleaseContentType.Movie,
                ReleaseGroup = new ReleaseGroup
                {
                    Name = $"Group {Guid.NewGuid():N}",
                    NumberOfHoursUntilReupload = 24,
                },
            },
            State = LinkCrypterImportState.ReadyForReview,
            CreatedAt = CreatedAt,
            UpdatedAt = CreatedAt,
            ErrorMessages = [],
        };
        dbContext.LinkCrypterImports.Add(linkCrypterImport);

        return linkCrypterImport;
    }

    private static LinkCrypterImport AddEpisodeImport(BearcatDbContext dbContext)
    {
        var linkCrypterImport = AddImport(dbContext);
        AddContainer(
            linkCrypterImport,
            "Show.S01E01-GRP",
            LinkCrypterImportContainerStatus.Importable
        );
        AddContainer(
            linkCrypterImport,
            "Show E01 backup",
            LinkCrypterImportContainerStatus.NotImportable,
            releaseName: "Show.S01E01-GRP"
        );
        AddContainer(
            linkCrypterImport,
            "Show.S01E02-GRP",
            LinkCrypterImportContainerStatus.Importable,
            isSelected: false
        );
        AddContainer(
            linkCrypterImport,
            "Show.S01E03-GRP",
            LinkCrypterImportContainerStatus.NotImportable
        );
        AddContainer(linkCrypterImport, "Movie-GRP", LinkCrypterImportContainerStatus.Importable);

        return linkCrypterImport;
    }

    private static void AddContainer(
        LinkCrypterImport linkCrypterImport,
        string containerName,
        LinkCrypterImportContainerStatus status,
        bool? isSelected = null,
        string? releaseName = null
    )
    {
        linkCrypterImport.Containers.Add(
            new LinkCrypterImportContainer
            {
                ContainerId = containerName,
                ContainerUrl = $"https://crypter.test/{Guid.NewGuid():N}",
                ContainerName = containerName,
                ReleaseName = releaseName ?? containerName,
                ReleaseTemplate = linkCrypterImport.ReleaseTemplate,
                ExternalReference = containerName,
                Status = status,
                IsSelected = isSelected ?? status is LinkCrypterImportContainerStatus.Importable,
                NotImportableReason =
                    status is LinkCrypterImportContainerStatus.NotImportable
                        ? LinkCrypterImportNotImportableReason.NoMirrorForReleaseHoster
                        : null,
                ErrorMessages = [],
                PreviewJson = new LinkCrypterImportContainerPreview(
                    Mirrors: [],
                    ReleaseCollectionName: null,
                    Releases:
                    [
                        new LinkCrypterImportReleasePreview(
                            ReleaseName: releaseName ?? containerName,
                            Mirrors: [],
                            ReleaseFolderPath: $"/releases/{containerName}",
                            ArchiveFolderPath: null,
                            ArchiveFolderContainsArchivesOfOtherReleases: false,
                            ReleaseAction: LinkCrypterImportReleaseAction.CreateRelease,
                            ExistingReleaseId: null,
                            NotImportableReason: null
                        ),
                    ]
                ).ToJson(),
            }
        );
    }

    private static HosterRegistration CreateHosterRegistration(string name, string hosterClassName)
    {
        return new HosterRegistration
        {
            Name = name,
            SerializedConfig = "{}",
            HosterClassName = hosterClassName,
            IsActive = true,
        };
    }
}
