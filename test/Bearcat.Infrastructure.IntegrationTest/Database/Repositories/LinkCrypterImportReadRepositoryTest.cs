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
    public async Task SearchContainersAsync_StatusFilterAndPaging_ReturnsMatchingContainersOfImportByName()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var linkCrypterImport = AddImport(dbContext);
        AddContainer(linkCrypterImport, "Charlie", LinkCrypterImportContainerStatus.Importable);
        AddContainer(linkCrypterImport, "Alpha", LinkCrypterImportContainerStatus.NotImportable);
        AddContainer(linkCrypterImport, "Bravo", LinkCrypterImportContainerStatus.Importable);
        AddContainer(linkCrypterImport, "Delta", LinkCrypterImportContainerStatus.AlreadyInBearcat);
        var otherImport = AddImport(dbContext);
        AddContainer(otherImport, "Echo", LinkCrypterImportContainerStatus.Importable);
        await dbContext.SaveChangesAsync();
        var repository = new LinkCrypterImportReadRepository(CreateDbContext());

        // Act
        var result = await repository.SearchContainersAsync(
            new LinkCrypterImportContainerSearchQuery(
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
        result.TotalCount.ShouldBe(3);
        result
            .Items.Select(container => container.ContainerName)
            .ShouldBe(["Alpha", "Bravo", "Charlie"]);
        result
            .Items.Single(container => container.ContainerName == "Alpha")
            .NotImportableReason.ShouldBe(
                LinkCrypterImportNotImportableReason.NoMirrorForReleaseTemplateHoster
            );
        result
            .Items.Single(container => container.ContainerName == "Bravo")
            .Preview.ReleaseFolderPath.ShouldBe("/releases/Bravo");
    }

    [Test]
    public async Task GetDetailAsync_ImportWithHosterChoice_ReturnsCountsPerStatusAndAccountsOfHoster()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var linkCrypterImport = AddImport(dbContext);
        AddContainer(linkCrypterImport, "Alpha", LinkCrypterImportContainerStatus.Importable);
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
                (LinkCrypterImportContainerStatus.Importable, 2),
                (LinkCrypterImportContainerStatus.NotImportable, 1),
            ]);
        result.SelectedContainerCount.ShouldBe(2);
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
        LinkCrypterImportContainerStatus status
    )
    {
        linkCrypterImport.Containers.Add(
            new LinkCrypterImportContainer
            {
                ContainerId = containerName,
                ContainerUrl = $"https://crypter.test/{Guid.NewGuid():N}",
                ContainerName = containerName,
                ExternalReference = containerName,
                Status = status,
                IsSelected = status is LinkCrypterImportContainerStatus.Importable,
                NotImportableReason =
                    status is LinkCrypterImportContainerStatus.NotImportable
                        ? LinkCrypterImportNotImportableReason.NoMirrorForReleaseTemplateHoster
                        : null,
                ErrorMessages = [],
                PreviewJson = new LinkCrypterImportContainerPreview(
                    Mirrors: [],
                    ReleaseFolderPath: $"/releases/{containerName}"
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
