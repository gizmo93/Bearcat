using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.ManageReleaseCollections.Overview;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.ManageReleaseCollections.Overview;

public class CollectionLinkContainerOverviewServiceTest
{
    [Test]
    public void HasSharedLinkCryptersOrContainers_NeitherSharedLinkCryptersNorContainers_ReturnsFalse()
    {
        // Act
        var result = CollectionLinkContainerOverviewService.HasSharedLinkCryptersOrContainers(
            CreateUploadSlot()
        );

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void HasSharedLinkCryptersOrContainers_OnlySharedLinkCrypters_ReturnsTrue()
    {
        // Act
        var result = CollectionLinkContainerOverviewService.HasSharedLinkCryptersOrContainers(
            CreateUploadSlot(sharedLinkCrypters: [CreateSharedLinkCrypter("Filecrypt")])
        );

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void HasSharedLinkCryptersOrContainers_OnlyContainers_ReturnsTrue()
    {
        // Act
        var result = CollectionLinkContainerOverviewService.HasSharedLinkCryptersOrContainers(
            CreateUploadSlot(
                containers:
                [
                    CreateContainer(
                        "Filecrypt",
                        LinkCrypterContainerState.Created,
                        "https://filecrypt.example/a"
                    ),
                ]
            )
        );

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void GetSharedLinkCryptersWithoutContainer_SomeCryptersHaveContainers_ReturnsCryptersWithoutContainer()
    {
        // Arrange
        var keeplinks = CreateSharedLinkCrypter("Keeplinks");
        var uploadSlot = CreateUploadSlot(
            sharedLinkCrypters: [CreateSharedLinkCrypter("Filecrypt"), keeplinks],
            containers:
            [
                CreateContainer(
                    "Filecrypt",
                    LinkCrypterContainerState.CreationFailed,
                    string.Empty
                ),
            ]
        );

        // Act
        var result = CollectionLinkContainerOverviewService.GetSharedLinkCryptersWithoutContainer(
            uploadSlot
        );

        // Assert
        result.ShouldBe([keeplinks]);
    }

    [Test]
    public void GroupCreatedContainerUrlsByLinkCrypter_ContainersOfSeveralSlots_ReturnsDistinctCreatedUrlsPerCrypter()
    {
        // Arrange
        var uploadSlots = new List<CollectionUploadSlotReadModel>
        {
            CreateUploadSlot(
                containers:
                [
                    CreateContainer(
                        "Filecrypt",
                        LinkCrypterContainerState.Created,
                        "https://filecrypt.example/a"
                    ),
                    CreateContainer(
                        "Keeplinks",
                        LinkCrypterContainerState.CreationFailed,
                        "https://keeplinks.example/failed"
                    ),
                ]
            ),
            CreateUploadSlot(
                containers:
                [
                    CreateContainer(
                        "Filecrypt",
                        LinkCrypterContainerState.Created,
                        "https://filecrypt.example/b"
                    ),
                    CreateContainer(
                        "Filecrypt",
                        LinkCrypterContainerState.Created,
                        "https://filecrypt.example/a"
                    ),
                    CreateContainer("Filecrypt", LinkCrypterContainerState.Created, " "),
                ]
            ),
        };

        // Act
        var result = CollectionLinkContainerOverviewService.GroupCreatedContainerUrlsByLinkCrypter(
            uploadSlots
        );

        // Assert
        result.Count.ShouldBe(2);
        result[0].LinkCrypterRegistrationName.ShouldBe("Filecrypt");
        result[0]
            .ContainerUrls.ShouldBe(["https://filecrypt.example/a", "https://filecrypt.example/b"]);
        result[1].LinkCrypterRegistrationName.ShouldBe("Keeplinks");
        result[1].ContainerUrls.ShouldBeEmpty();
    }

    private static CollectionUploadSlotReadModel CreateUploadSlot(
        IReadOnlyList<CollectionUploadSlotLinkCrypterReadModel>? sharedLinkCrypters = null,
        IReadOnlyList<CollectionUploadSlotContainerReadModel>? containers = null
    ) =>
        new(
            1,
            "rapidgator",
            "Rapidgator",
            false,
            CollectionUploadSlotPasswordPolicy.Ignore,
            null,
            2,
            2,
            sharedLinkCrypters ?? [],
            containers ?? []
        );

    private static CollectionUploadSlotLinkCrypterReadModel CreateSharedLinkCrypter(
        string linkCrypterRegistrationName
    ) => new(1, linkCrypterRegistrationName, true, null, false, false, false, 1);

    private static CollectionUploadSlotContainerReadModel CreateContainer(
        string linkCrypterRegistrationName,
        LinkCrypterContainerState state,
        string containerUrl
    ) =>
        new(
            1,
            linkCrypterRegistrationName,
            containerUrl,
            state,
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            1,
            []
        );
}
