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
                LinkCrypterImportId: linkCrypterImport.Id,
                Statuses:
                [
                    LinkCrypterImportContainerStatus.Importable,
                    LinkCrypterImportContainerStatus.NotImportable,
                ],
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
        releaseA.HasImportableContainers.ShouldBeTrue();
        releaseA.ReleaseFolderPath.ShouldBe("/releases/Release.A");
        var releaseB = result.Items[1];
        releaseB
            .Containers.Select(container => container.Status)
            .ShouldBe([
                LinkCrypterImportContainerStatus.NotImportable,
                LinkCrypterImportContainerStatus.AlreadyInBearcat,
            ]);
        releaseB.HasImportableContainers.ShouldBeFalse();
        result.Items[2].IsSelected.ShouldBeTrue();
    }

    [Test]
    public async Task GetDetailAsync_ImportWithHosterChoice_ReturnsCountsPerStatusAndSelectedReleasesAndAccountsOfHoster()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var linkCrypterImport = AddImport(dbContext);
        AddContainer(linkCrypterImport, "Alpha", LinkCrypterImportContainerStatus.Importable);
        AddContainer(linkCrypterImport, "alpha", LinkCrypterImportContainerStatus.Importable);
        AddContainer(linkCrypterImport, "Bravo", LinkCrypterImportContainerStatus.Importable);
        AddContainer(linkCrypterImport, "Charlie", LinkCrypterImportContainerStatus.NotImportable);
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
        result.SelectedContainerCount.ShouldBe(3);
        result.SelectedReleaseCount.ShouldBe(2);
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

    private static void AddContainer(
        LinkCrypterImport linkCrypterImport,
        string containerName,
        LinkCrypterImportContainerStatus status,
        bool isSelected = true
    )
    {
        linkCrypterImport.Containers.Add(
            new LinkCrypterImportContainer
            {
                ContainerId = containerName,
                ContainerUrl = $"https://crypter.test/{Guid.NewGuid():N}",
                ContainerName = containerName,
                ReleaseName = containerName,
                ExternalReference = containerName,
                Status = status,
                IsSelected = isSelected && status is LinkCrypterImportContainerStatus.Importable,
                NotImportableReason =
                    status is LinkCrypterImportContainerStatus.NotImportable
                        ? LinkCrypterImportNotImportableReason.NoMirrorForReleaseHoster
                        : null,
                ErrorMessages = [],
                PreviewJson = new LinkCrypterImportContainerPreview(
                    Mirrors: [],
                    ReleaseFolderPath: $"/releases/{containerName}",
                    ArchiveFolderPath: null,
                    ReleaseAction: LinkCrypterImportReleaseAction.CreateRelease,
                    ExistingReleaseId: null
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
